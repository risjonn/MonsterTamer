using System;
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

        public HitFlashAnimation(float cx, float cy) : base(25)
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

    // Green flash on the healing monster — bright yellow-green pulse fading to green.
    public class HealFlashAnimation : BattleAnimation
    {
        private float _cx, _cy;

        public HealFlashAnimation(float cx, float cy) : base(25)
        {
            _cx = cx;
            _cy = cy;
        }

        public override void Draw()
        {
            float fade  = 1f - (float)_frame / _duration;
            int   alpha = (int)(fade * 170);
            Color flash = _frame <= 3
                ? SplashKit.RGBAColor(200, 255, 100, alpha)
                : SplashKit.RGBAColor(60,  210,  80, alpha);
            SplashKit.FillCircle(flash, _cx, _cy, 72f);
        }
    }

    // Floating "+N HP" text in green that rises and fades — mirrors DamageNumberAnimation.
    public class HealNumberAnimation : BattleAnimation
    {
        private float  _cx, _startY;
        private int    _healAmount;

        public HealNumberAnimation(float cx, float startY, int healAmount) : base(100)
        {
            _cx        = cx;
            _startY    = startY;
            _healAmount = healAmount;
        }

        public override void Draw()
        {
            float  t     = (float)_frame / _duration;
            float  y     = _startY - t * 55f;
            int    alpha = (int)((1f - t) * 255);
            int    fsize = 22 - (int)(t * 5);
            string text  = $"+{_healAmount} HP";
            int    tw    = SplashKit.TextWidth(text, "Arial", fsize);
            SplashKit.DrawText(text,
                SplashKit.RGBAColor(60, 220, 80, alpha), "Arial", fsize,
                (int)(_cx - tw / 2f), (int)y);
        }
    }

    // 4 rising green particles that spread outward and fade — visual flair for heal moves.
    public class HealParticleAnimation : BattleAnimation
    {
        private readonly (float ox, float drift)[] _particles;
        private float _cx, _cy;

        public HealParticleAnimation(float cx, float cy) : base(75)
        {
            _cx = cx;
            _cy = cy;
            _particles = new (float, float)[]
            {
                (-22f, -0.30f),
                ( -8f,  0.10f),
                (  8f,  0.40f),
                ( 22f, -0.15f),
            };
        }

        public override void Draw()
        {
            float t     = (float)_frame / _duration;
            int   alpha = (int)((1f - t) * 220);
            float rise  = t * 65f;
            float radius = 5f * (1f - t * 0.5f);

            foreach (var (ox, drift) in _particles)
            {
                float px = _cx + ox + drift * _frame;
                float py = _cy - rise;
                SplashKit.FillCircle(SplashKit.RGBAColor(80, 220, 90, alpha), px, py, radius);
            }
        }
    }

    // Swirling coloured circles around the target + a floating status label.
    // Colour encodes the status: yellow = Paralysed, orange = Burned, blue = Frozen, purple = Poisoned.
    public class StatusSwirlAnimation : BattleAnimation
    {
        private float _cx, _cy;
        private int   _r, _g, _b;
        private string _label;

        public StatusSwirlAnimation(float cx, float cy, StatusCondition status) : base(90)
        {
            _cx = cx;
            _cy = cy;
            (_r, _g, _b, _label) = status switch
            {
                StatusCondition.Paralysed => (255, 215,   0, "PARALYSED!"),
                StatusCondition.Burned    => (255, 120,  20, "BURNED!"),
                StatusCondition.Frozen    => ( 80, 180, 255, "FROZEN!"),
                StatusCondition.Poisoned  => (160,  55, 220, "POISONED!"),
                _                         => (200, 200, 200, "STATUS!"),
            };
        }

        public override void Draw()
        {
            float t        = (float)_frame / _duration;
            int   swAlpha  = (int)((1f - t) * 190);
            float orbitR   = 52f + t * 12f;

            for (int i = 0; i < 3; i++)
            {
                double angle = 2 * Math.PI * i / 3 + _frame * 0.14;
                float  px    = _cx + (float)(Math.Cos(angle) * orbitR);
                float  py    = _cy + (float)(Math.Sin(angle) * orbitR * 0.45f);
                SplashKit.FillCircle(SplashKit.RGBAColor(_r, _g, _b, swAlpha), px, py, 9f);
            }

            // Floating label: stays visible for first 70 % of duration then fades
            float labelY    = _cy - 65f - t * 25f;
            float labelFade = t < 0.70f ? 1f : (1f - t) / 0.30f;
            int   lblAlpha  = (int)(labelFade * 255);
            int   tw        = SplashKit.TextWidth(_label, "Arial", 19);
            SplashKit.DrawText(_label,
                SplashKit.RGBAColor(_r, _g, _b, lblAlpha), "Arial", 19,
                (int)(_cx - tw / 2f), (int)labelY);
        }
    }

    // Multi-phase capture orb: travels from player to enemy, expands on contact, then shows result.
    // Phase 1 (0-40):  orb travels with an upward arc.
    // Phase 2 (41-65): orb expands as a blue flash on contact.
    // Phase 3 (66-130): "Caught!" (gold) for Caught; "Escaped!" (red) for Escaped or Failed.
    public class CaptureOrbAnimation : BattleAnimation
    {
        private readonly float         _sx, _sy, _tx, _ty;
        private readonly CaptureResult _result;

        private const int TravelEnd = 40;
        private const int ExpandEnd = 65;

        public CaptureOrbAnimation(float sx, float sy, float tx, float ty, CaptureResult result)
            : base(130)
        {
            _sx = sx; _sy = sy;
            _tx = tx; _ty = ty;
            _result = result;
        }

        public override void Draw()
        {
            if (_frame <= TravelEnd)
            {
                float t   = (float)_frame / TravelEnd;
                float cx  = _sx + (_tx - _sx) * t;
                float arc = (float)(Math.Sin(t * Math.PI) * -50.0);
                float cy  = _sy + (_ty - _sy) * t + arc;
                SplashKit.FillCircle(SplashKit.RGBAColor(100, 140, 255, 80), cx, cy, 18f);
                SplashKit.FillCircle(SplashKit.RGBColor(50, 80, 210), cx, cy, 13f);
                SplashKit.FillCircle(SplashKit.RGBAColor(220, 235, 255, 150), cx - 4f, cy - 4f, 5f);
            }
            else if (_frame <= ExpandEnd)
            {
                float t      = (float)(_frame - TravelEnd) / (ExpandEnd - TravelEnd);
                float radius = 13f + t * 60f;
                int   alpha  = (int)((1f - t) * 200);
                SplashKit.FillCircle(SplashKit.RGBAColor(100, 150, 255, alpha), _tx, _ty, radius);
            }
            else
            {
                float t       = (float)(_frame - ExpandEnd) / (_duration - ExpandEnd);
                float fadeIn  = Math.Min(1f, t / 0.12f);
                float fadeOut = t > 0.75f ? (1f - t) / 0.25f : 1f;
                int   alpha   = (int)(Math.Min(fadeIn, fadeOut) * 255f);
                float y       = _ty - 55f - t * 40f;

                string text  = _result == CaptureResult.Caught ? "Caught!" : "Escaped!";
                Color  color = _result == CaptureResult.Caught
                    ? SplashKit.RGBAColor(255, 215,  0, alpha)
                    : SplashKit.RGBAColor(255,  70, 70, alpha);

                int tw = SplashKit.TextWidth(text, "Arial", 28);
                SplashKit.DrawText(text, color, "Arial", 28, (int)(_tx - tw / 2f), (int)y);
            }
        }
    }

    // Sustained pulsing green glow for potion use — two concentric circles animated with a sine wave.
    // Pair with HealNumberAnimation for the floating "+X HP" text.
    public class PotionGlowAnimation : BattleAnimation
    {
        private float _cx, _cy;

        public PotionGlowAnimation(float cx, float cy) : base(75)
        {
            _cx = cx;
            _cy = cy;
        }

        public override void Draw()
        {
            float t      = (float)_frame / _duration;
            float pulse  = (float)(Math.Sin(_frame * 0.22) * 0.4 + 0.6);
            float fade   = 1f - t;
            int   outerA = (int)(pulse * 110 * fade);
            int   innerA = (int)(pulse * 170 * fade);
            SplashKit.FillCircle(SplashKit.RGBAColor(40, 180, 60, outerA), _cx, _cy, 86f);
            SplashKit.FillCircle(SplashKit.RGBAColor(60, 220, 80, innerA), _cx, _cy, 68f);
        }
    }

    // Brief status-coloured flash + rising "SKIPPED!" text for frozen / paralysed turn skips.
    public class TurnSkipAnimation : BattleAnimation
    {
        private float  _cx, _cy;
        private int    _r, _g, _b;

        public TurnSkipAnimation(float cx, float cy, StatusCondition status) : base(75)
        {
            _cx = cx;
            _cy = cy;
            (_r, _g, _b) = status switch
            {
                StatusCondition.Paralysed => (255, 215,   0),
                StatusCondition.Frozen    => ( 80, 180, 255),
                _                         => (180, 180, 180),
            };
        }

        public override void Draw()
        {
            float t = (float)_frame / _duration;

            // Short flash during first ~18 % of duration
            if (_frame < 10)
            {
                float flashFade = 1f - _frame / 10f;
                SplashKit.FillCircle(SplashKit.RGBAColor(_r, _g, _b, (int)(flashFade * 155)),
                    _cx, _cy, 72f);
            }

            // "SKIPPED!" rises and fades
            float  y     = _cy - 45f - t * 42f;
            int    alpha = (int)((1f - t) * 255);
            string text  = "SKIPPED!";
            int    tw    = SplashKit.TextWidth(text, "Arial", 21);
            SplashKit.DrawText(text,
                SplashKit.RGBAColor(_r, _g, _b, alpha), "Arial", 21,
                (int)(_cx - tw / 2f), (int)y);
        }
    }
}
