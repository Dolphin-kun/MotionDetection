using OpenCvSharp;
using System.Runtime.InteropServices;
using Vortice.DCommon;
using Vortice.Direct2D1;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace MotionDetection
{
    public static class BitmapToMat
    {
        public static Mat BitmapToOpenCvMat(ID2D1DeviceContext dc, ID2D1Bitmap bitmap)
        {
            var bitmap1 = bitmap.QueryInterfaceOrNull<ID2D1Bitmap1>();
            ID2D1Bitmap1 cpuBitmap;

            if (bitmap1 != null && (bitmap1.Options & BitmapOptions.CpuRead) != 0)
            {
                cpuBitmap = bitmap1;
            }
            else
            {
                bitmap1?.Dispose();

                var prop = new BitmapProperties1(
                    new Vortice.DCommon.PixelFormat(Vortice.DXGI.Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied),
                    96, 96,
                    BitmapOptions.CpuRead | BitmapOptions.CannotDraw);

                cpuBitmap = dc.CreateBitmap(bitmap.PixelSize, prop);
                cpuBitmap.CopyFromBitmap(bitmap);
            }

            var mapped = cpuBitmap.Map(MapOptions.Read);
            Mat mat;

            try
            {
                int w = cpuBitmap.PixelSize.Width;
                int h = cpuBitmap.PixelSize.Height;
                using var tmp = new Mat(h, w, MatType.CV_8UC4, mapped.Bits, mapped.Pitch);
                mat = tmp.Clone();
            }
            finally
            {
                cpuBitmap.Unmap();
                cpuBitmap.Dispose();
            }

            return mat;
        }

        public static ID2D1Bitmap1 CreateD2DBitmapFromMat(ID2D1DeviceContext dc, Mat mat)
        {
            if (mat.Type() != MatType.CV_8UC4)
                throw new ArgumentException("Mat type must be CV_8UC4 (BGRA32)");

            int byteSize = mat.Rows * mat.Cols * mat.ElemSize();
            byte[] buffer = new byte[byteSize];
            Marshal.Copy(mat.Data, buffer, 0, buffer.Length);

            var size = new SizeI(mat.Width, mat.Height);
            var props = new BitmapProperties1(
                new PixelFormat(Format.B8G8R8A8_UNorm, AlphaMode.Premultiplied),
                96, 96,
                BitmapOptions.None);

            return dc.CreateBitmap(size, props);
        }

    }
}
