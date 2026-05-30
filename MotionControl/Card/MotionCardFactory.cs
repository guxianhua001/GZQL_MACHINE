using Core.Utilities;
using MotionControl.Interfaces;

namespace MotionControl.Card
{
    public class MotionCardFactory : IMotionCardFactory
    {
        private readonly ILoggerService _logger = new LoggerService();
        private readonly Dictionary<int, IMotionCard> _cache = new();
        private int _cardCount; // 实际检测到的卡数量

        public MotionCardFactory(ILoggerService logger)
        {
            _logger = logger;
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
                    _logger.Error($"MotionCardFactory: 初始化卡 {index} 失败，错误码：{initResult}");
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