using Core.Abstraction;
using Core.Utilities;
using MotionControl.Interfaces;

namespace MotionControl.Card
{
    public class MotionCardFactory : IMotionCardFactory
    {
        private readonly ILoggerService _logger = new LoggerService();
        private readonly Dictionary<int, IMotionCard> _cache = new();
        private int _cardCount; // 实际检测到的卡数量
        /// <summary> 本地化服务，用于日志多语言支持 </summary>
        private readonly ILocalizationService _localization;

        public MotionCardFactory(ILoggerService logger, ILocalizationService localization)
        {
            _logger = logger;
            _localization = localization;
            // 立刻扫描硬件获取卡数量，不初始化具体卡
            _cardCount = ScanCardCount();
        }

        public IMotionCard? GetCard(int index)
        {
            if (index < 0 || index >= _cardCount)
                return null;   // 无硬件时直接返回 null

            if (!_cache.TryGetValue(index, out var card))
            {
                card = new LeisaiMotionCard(index);
                int initResult = card.Initialize();
                if (initResult != 0)
                {
                    _logger.Error(string.Format(_localization.GetResourceOrDefault("MCF_Log_InitCardFailed", "MotionCardFactory: 初始化卡 {0} 失败，错误码：{1}"), index, initResult));
                    return null;
                }
                _cache[index] = card;
            }
            return card;
        }

        public IMotionCard GetDefaultCard() => GetCard(0);

        public int CardCount => _cardCount;

        private int ScanCardCount()
        {
            // 快速扫描一次获取卡总数，不占用卡对象
            try
            {
                LTDMC.dmc_board_close();
                int num = LTDMC.dmc_board_init();
                return (num > 0 && num <= 8) ? num : 0;
            }
            catch
            {
                return 0;
            }
        }
    }
}