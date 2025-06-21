using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.ItemEditor.CustomVisibilityAttributes;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace MotionDetection
{
    [VideoEffect("動体検知", ["合成"], ["Motion Detection"], isAviUtlSupported: false)]
    internal class MotionDetectionEffect : VideoEffectBase
    {
        public override string Label => "動体検知";

        [Display(GroupName = "動体検知", Name = "しきい値", Description = "しきい値")]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation Thresh { get; } = new Animation(75, 0, 100);

        [Display(GroupName = "動体検知", Name = "滑らかさ", Description = "滑らかさ")]
        [AnimationSlider("F0", "", 0, 10)]
        public Animation Blur { get; } = new Animation(0, 0, 50);

        [Display(GroupName = "動体検知", Name = "ノイズ除去強度", Description = "ノイズ除去強度")]
        [AnimationSlider("F0", "", 0, 10)]
        public Animation SkipNoiseSize { get; } = new Animation(0, 0, 100);

        [Display(GroupName = "動体検知", Name = "反転", Description = "反転")]
        [ToggleSlider]
        public bool Invert { get => invert; set => Set(ref invert, value); }
        bool invert = false;

        [Display(GroupName = "動体検知", Name = "クリッピング", Description = "クリッピング")]
        [ToggleSlider]
        public bool Crop { get => crop; set => Set(ref crop, value); }
        bool crop = false;

        [Display(GroupName = "動体検知", Name = "対象表示", Description = "対象表示")]
        [ToggleSlider]
        public bool RectLine { get => rectLine; set => Set(ref rectLine, value); }
        bool rectLine = true;

        [Display(GroupName = "動体検知", Name = "色", Description = "色")]
        [ColorPicker]
        [ShowPropertyEditorWhen(nameof(RectLine), true)]
        public Color Color { get => color; set => Set(ref color, value); }
        Color color = Colors.White;

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            return [];
        }

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        {
            return new MotionDetectionEffectProcessor(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [Thresh, Blur, SkipNoiseSize];
    }
}
