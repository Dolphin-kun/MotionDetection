using OpenCvSharp;
using System.Numerics;
using Vortice.DCommon;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using Vortice.DXGI;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;

namespace MotionDetection
{
    internal class MotionDetectionEffectProcessor : IVideoEffectProcessor
    {
        readonly DisposeCollector disposer = new();
        readonly IGraphicsDevicesAndContext devices;
        readonly MotionDetectionEffect item;

        ID2D1Image? inputCurrent;
        ID2D1Image? inputPrevious;
        ID2D1Bitmap1? processed;
        private readonly AffineTransform2D wrap;

        public ID2D1Image Output { get; private set; }

        public MotionDetectionEffectProcessor(IGraphicsDevicesAndContext devices, MotionDetectionEffect item)
        {
            this.devices = devices;
            this.item = item;

            wrap = new AffineTransform2D(devices.DeviceContext);
            disposer.Collect(wrap);
            Output = wrap.Output;
        }

        public DrawDescription Update(EffectDescription effectDescription)
        {
            if (inputCurrent is null || inputPrevious is null)
                return effectDescription.DrawDescription;

            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;

            double thresh = item.Thresh.GetValue(frame, length, fps) / 100 * 255.0;
            double blurSize = item.Blur.GetValue(frame, length, fps);
            double thickness = item.Thickness.GetValue(frame, length, fps);
            double skipNoiseSize = item.SkipNoiseSize.GetValue(frame, length, fps) * 10;

            var dc = devices.DeviceContext;
            var bounds = dc.GetImageLocalBounds(inputCurrent);
            int width = (int)(bounds.Right - bounds.Left);
            int height = (int)(bounds.Bottom - bounds.Top);

            using var currentBitmap = dc.CreateBitmap(new SizeI(width, height),
                new BitmapProperties1(new PixelFormat(Format.B8G8R8A8_UNorm, AlphaMode.Premultiplied), 96, 96, BitmapOptions.Target));

            using var prevBitmap = dc.CreateBitmap(new SizeI(width, height),
                new BitmapProperties1(new PixelFormat(Format.B8G8R8A8_UNorm, AlphaMode.Premultiplied), 96, 96, BitmapOptions.Target));

            dc.Target = currentBitmap;
            dc.BeginDraw();
            dc.Clear(null);
            dc.DrawImage(inputCurrent, new Vector2(-bounds.Left, -bounds.Top));
            dc.EndDraw();
            dc.Target = null;

            using var matCurr = BitmapToMat.BitmapToOpenCvMat(dc, currentBitmap);
            using var matPrev = BitmapToMat.BitmapToOpenCvMat(dc, prevBitmap);

            using var grayCurr = new Mat();
            using var grayPrev = new Mat();
            Cv2.CvtColor(matCurr, grayCurr, ColorConversionCodes.BGRA2GRAY);
            Cv2.CvtColor(matPrev, grayPrev, ColorConversionCodes.BGRA2GRAY);

            int kernelSizeBlur = 3 + 2 * ((int)blurSize - 1);
            Cv2.GaussianBlur(grayCurr, grayCurr, new OpenCvSharp.Size(kernelSizeBlur, kernelSizeBlur), 0);
            Cv2.GaussianBlur(grayPrev, grayPrev, new OpenCvSharp.Size(kernelSizeBlur, kernelSizeBlur), 0);

            using var diff = new Mat();
            Cv2.Absdiff(grayCurr, grayPrev, diff);

            using var binary = new Mat();
            Cv2.Threshold(diff, binary, thresh, 255, ThresholdTypes.Binary);

            using var inverted = new Mat();
            if (item.Invert)
                binary.CopyTo(inverted);
            else
               Cv2.BitwiseNot(binary, inverted); 

            using var result = new Mat(matCurr.Rows, matCurr.Cols, MatType.CV_8UC4);

            if (item.Crop)
            {
                result.SetTo(Scalar.All(0));
                matCurr.CopyTo(result, inverted);
            }
            else
            {
                matCurr.CopyTo(result);
            }

            if (item.RectLine)
            {
                Cv2.FindContours(inverted, out Point[][] contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

                foreach (var contour in contours)
                {
                    if (Cv2.ContourArea(contour) < skipNoiseSize) continue;
                    var rect = Cv2.BoundingRect(contour);
                    Cv2.Rectangle(result, rect, new Scalar(item.Color.B, item.Color.G, item.Color.R, item.Color.A), (int)thickness);
                }
            }

            if (processed == null || processed.PixelSize.Width != width || processed.PixelSize.Height != height)
            {
                disposer.RemoveAndDispose(ref processed);
                processed = dc.CreateBitmap(
                    new SizeI(width, height),
                    nint.Zero,
                    0,
                    new BitmapProperties1(new PixelFormat(Format.B8G8R8A8_UNorm, AlphaMode.Premultiplied), 96, 96, BitmapOptions.None));
                disposer.Collect(processed);
            }


            processed.CopyFromMemory(result.Data, (int)result.Step());

            var processedRange = dc.GetImageLocalBounds(processed);
            var x = -(processedRange.Left + processedRange.Right) / 2;
            var y = -(processedRange.Top + processedRange.Bottom) / 2;
            wrap.TransformMatrix = Matrix3x2.CreateTranslation(x, y);
            wrap.SetInput(0, processed, true);


            inputPrevious = processed;

            return effectDescription.DrawDescription;
        }

        public void ClearInput()
        {
            wrap.SetInput(0, null, true);
        }

        public void SetInput(ID2D1Image? input)
        {
            inputPrevious = inputCurrent ?? input;
            inputCurrent = input;
        }

        public void Dispose()
        {
            Output?.Dispose();
            disposer.RemoveAndDispose(ref processed);
            disposer.Dispose();
        }
    }
}
