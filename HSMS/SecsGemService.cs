
using Interfaces;
using Interfaces.Events;
using Prism.Events;
using Recipe.Events;

namespace HSMS
{
    public class SecsGemService : ISecsGemService
    {
        public SECS.SECS Secs { get; private set; }
        public bool IsConnect => Secs.IsConnect;
        public bool IsEnableSecs { get; set; }
        public string OutUnitCode { get; set; } = "";
        //public string CurrentRecipe { get; set; }
        //public List<string> RecipeList { get; set; } = new List<string>();
        public int UPH { get; set; }
        public int TotalCount { get; set; }
        public int recipe { get; set; }
        public int controlMode { get; set; }
        public bool hostToEqpHold { get; set; } 

        private short[,] _mapping;
        private string[,] _barcode;
        public short[,] GetMapping => _mapping;
        public string[,] GetSn => _barcode;
        private bool _isInitialized;
        private string _currentRecipe;
        private List<string> _recipeList = new List<string>();
        private readonly IEventAggregator _eventAggregator;
        private IRecipeManagerService _recipeManagerService;
        private bool _suppressEventHandling;

        public string CurrentRecipe
        {
            get => _currentRecipe;
            set
            {
                if (_currentRecipe == value) return;
                _currentRecipe = value;

                // 只有在外部驱动时才发出事件
                if (!_suppressEventHandling)
                {
                    _eventAggregator.GetEvent<RecipeChangedEvent>().Publish(value);
                }
            }
        }
        public List<string> RecipeList
        {
            get => _recipeList;
            set
            {
                _recipeList = value?.ToList() ?? new List<string>();
                IMessage.Logger.Info($"SecsGem配方列表更新: {_recipeList.Count}个配方");
            }
        }

        public SecsGemService(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
        }
        // 用于设置 recipeManagerService
        public void InitializeDependencies(IRecipeManagerService recipeManagerService)
        {
            _recipeManagerService = recipeManagerService;
            // 事件订阅
            _eventAggregator.GetEvent<RecipeChangedEvent>().Subscribe(OnRecipeChanged);
        }

        public void InitializeSECS()
        {
            if (Secs != null) return;

            Secs = new SECS.SECS(); // 创建时不传递依赖
            Secs.InitializeSECS(this, _eventAggregator, _recipeManagerService); // 初始化时传递服务实例

            IMessage.Logger.Info("SECS/GEM 服务初始化完成");
        }
        public void SetRecipeInfo(string currentRecipe, List<string> allRecipes)
        {
            try
            {
                // 设置配方信息
                CurrentRecipe = currentRecipe;
                RecipeList = allRecipes;

                IMessage.Logger.Info($"设置配方信息: 当前配方={currentRecipe}, 总共{allRecipes?.Count ?? 0}个配方");
            }
            catch (Exception ex)
            {
                IMessage.Logger.Error($"设置配方信息失败: {ex.Message}");
            }
        }
        private void OnRecipeChanged(string recipeName)
        {
            // 抑制自身变化引发的事件
            if (_suppressEventHandling) return;

            try
            {
                _suppressEventHandling = true;

                // 更新内部状态
                CurrentRecipe = recipeName;

                // 发送 SECS 消息
                SendRecipeChangedMessage(recipeName);
            }
            finally
            {
                _suppressEventHandling = false;
            }
        }
        private void SendRecipeChangedMessage(string recipeName)
        {
            try
            {
                if (Secs == null) return;

                // 实现发送配方更改的SECS消息逻辑
                bool success = Secs.SendRecipeChangedMessage(recipeName);

                IMessage.Logger.Info($"已向主机发送配方更改通知: {recipeName} ({(success ? "成功" : "失败")})");
            }
            catch (Exception ex)
            {
                IMessage.Logger.Error($"发送配方更改消息失败: {ex.Message}");
            }
        }

        // 当配方列表变化时调用
        public void OnRecipeListChanged(List<string> recipeNames)
        {
            RecipeList = recipeNames;

            // 如果当前配方不在列表中，重置为默认或第一个配方
            if (!RecipeList.Contains(CurrentRecipe) && RecipeList.Any())
            {
                CurrentRecipe = RecipeList.First();
            }

            IMessage.Logger.Info($"配方列表已更新: {string.Join(", ", RecipeList)}");
        }
        public void ProcessRemoteRecipeChange(string recipeName)
        {
            _eventAggregator.GetEvent<RecipeSelectionEvent>()
                .Publish(new RecipeSelectionParams { RecipeName = recipeName });
        }

        public event EventHandler<ConnectionStatusChangedEventArgs> ConnectionStatusChanged;
        protected virtual void OnConnectionStatusChanged(bool isConnected, string statusText)
        {
            ConnectionStatusChanged?.Invoke(this, new ConnectionStatusChangedEventArgs
            {
                IsConnected = isConnected,
                StatusText = statusText
            });
        }
        private int _port;
        private string _deviceId;
        public bool Initialize(int port, string deviceId)
        {
            if (_isInitialized) return true;
            if (!IsEnableSecs) return false;
            // 实现SECS/GEM初始化逻辑
            if (!Secs.IniSECS())
            {
                //throw new System.Exception("SECS初始化失败");
                return false;
            }
            _isInitialized = true;
            return _isInitialized;
        }
        public void SetEnabled(bool enabled)
        {
            if (!_isInitialized) return;
            IsEnableSecs = enabled;

            // 实现启用/禁用逻辑
            if (enabled)
            {
                OnConnectionStatusChanged(true, "连接中...");
                ConnectSECS();
                IMessage.Logger.Info("正在连接 SECS/GEM...");
            }
            else
            {
                CloseSECS();
                OnConnectionStatusChanged(false, "已断开");
                IMessage.Logger.Info("SECS/GEM 连接已关闭");
            }
        }

        public void ConnectSECS()
        {
            try
            {
                if (Secs == null) return;

                if (!Secs.BOpen)
                    Secs.OpenSecs();
                else
                    Secs.Connect();

                OnConnectionStatusChanged(Secs.IsConnect, Secs.IsConnect ? "已连接" : "连接失败");
            }
            catch (Exception ex)
            {
                OnConnectionStatusChanged(false, $"连接错误: {ex.Message}");
            }
        }

        public void CloseSECS()
        {
            if (Secs == null) return;
            Secs.CloseSecs();
            _isInitialized = false;
        }

        /// <summary>
        /// 上传报警
        /// </summary>
        /// <param name="alarmId"></param>
        public void UploadAlarmProcess(int alarmId)
        {
            if (Secs != null)
            {
                if (!IsEnableSecs) return;
                    Secs.errorsend((uint)alarmId);
            }
        }
        public void ClearAlarm()
        {
            if (Secs != null)
            {
                try
                {
                    if (!IsEnableSecs) return;
                        Secs.errorClear("");
                }
                catch (Exception ex)
                {
                    IMessage.Logger.Error($"【Host->Eqp】清除报警失败: {ex.Message}");
                }
            }
        }
        public void ClearAllAlarm()
        {
            if (Secs != null)
            {
                try
                {
                    if (!IsEnableSecs) return;
                       Secs.errorClear_All();
                }
                catch (Exception ex)
                {
                    IMessage.Logger.Error($"【Host->Eqp】清除报警失败: {ex.Message}");
                }
            }
        }
        //获取mapping
        public bool GetMappingStus(string code)
        {
            try
            {
                return Secs.GetMaping(code, out _mapping, out _barcode);
            }
            catch(Exception ex)
            {
                IMessage.Logger.Error($"【Host->Eqp】获取map失败: {ex.Message}");
                return false;
            }

        }

        public bool GetMappingStus(string code, out short[,] mapping, out string[,] barcode)
        {
            throw new NotImplementedException();
        }
        /// <summary>
        /// 处理报警信息委托
        /// 例如上传报警等
        /// </summary>
        public Action<int> UploadAlarmAction = null;

    }
}
