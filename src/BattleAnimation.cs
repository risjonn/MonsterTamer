using SplashKitSDK;

namespace MonsterTamer
{
    // Abstract base — each concrete animation owns its own frame counter and duration.
    // GameManager holds a List<BattleAnimation>, updates all each frame, removes completed ones.
    public abstract class BattleAnimation
    {
        protected int _frame;
        protected int _duration;

        protected BattleAnimation(int duration)
        {
            _frame    = 0;
            _duration = duration;
        }

        public bool IsComplete => _frame >= _duration;

        public void Update()
        {
            if (!IsComplete) _frame++;
        }

        public abstract void Draw();
    }

    // 5-frame white flash that fades to red — drawn as a semi-transparent circle over the target sprite.
    // Hook: instantiate on AttackHit event with target sprite centre coordinates.
    // Future reuse: Heal (green), Status (purple), Capture (yellow), Potion (cyan), TurnSkip (grey).
    public class HitFlashAnimation : BattleAnimation
    {
        private float _cx, _cy;

        public HitFlashAnimation(float cx, float cy) : base(20)
        {
            _cx = cx;
            _cy = cy;
        }

        public override void Draw()
        {
            float fade  = 1f - (float)_frame / _duration;
            int   alpha = (int)(fade * 175);
            // First 2 frames: bright white impact; remaining: fading red
            Color flash = _frame <= 2
                ? SplashKit.RGBAColor(255, 255, 255, alpha)
                : SplashKit.RGBAColor(255,  55,  55, alpha);
            SplashKit.FillCircle(flash, _cx, _cy, 70f);
        }
    }

    // Floating "-N" damage number that rises 55px and fades out over 40 frames.
    // Hook: instantiate on AttackHit event, pass damage amount and target sprite centre.
    public class DamageNumberAnimation : BattleAnimation
    {
        private float _cx, _startY;
        private int   _damage;

        public DamageNumberAnimation(float cx, float startY, int damage) : base(100)
        {
            _cx     = cx;
            _startY = startY;
            _damage = damage;
        }

        public override void Draw()
        {
            float t     = (float)_frame / _duration;
            float y     = _startY - t * 55f;
            int   alpha = (int)((1f - t) * 255);
            int   fsize = 22 - (int)(t * 5);   // shrinks slightly as it fades
            string text = $"-{_damage}";
            int    tw   = SplashKit.TextWidth(text, "Arial", fsize);
            SplashKit.DrawText(text,
                SplashKit.RGBAColor(255, 60, 60, alpha), "Arial", fsize,
                (int)(_cx - tw / 2f), (int)y);
        }
    }
}
