using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LEDControl
{
    class ScreenColors
    {
        public ScreenColors()
        {
            setupDX();
        }
        public static Bitmap getScreenGDI(int screen = 1)
        {
            //http://stackoverflow.com/questions/362986/capture-the-screen-into-a-bitmap
            //Create a new bitmap.
            var bounds = System.Windows.Forms.Screen.AllScreens[screen].Bounds;
            var bmpScreenshot = new Bitmap(bounds.Width,
                                           bounds.Height,
                                           PixelFormat.Format24bppRgb);

            // Create a graphics object from the bitmap.
            var gfxScreenshot = Graphics.FromImage(bmpScreenshot);

            // Take the screenshot from the upper left corner to the right bottom corner.
            gfxScreenshot.CopyFromScreen(bounds.X,
                                        bounds.Y,
                                        0,
                                        0,
                                        bounds.Size,
                                        CopyPixelOperation.SourceCopy);

            gfxScreenshot.Dispose();

            // Save the screenshot to the specified path that the user has chosen.
            //bmpScreenshot.Save("Screenshot.png", ImageFormat.Png);
            return bmpScreenshot;
        }

        SharpDX.Direct3D11.Texture2D screenTexture;
        public SharpDX.Direct3D11.Device device;
        SharpDX.DXGI.OutputDuplication duplicatedOutput;
        public void setupDX()
        {
            try
            {
                // # of graphics card adapter
                const int numAdapter = 0;

                // # of output device (i.e. monitor)
                const int numOutput = 0;

                //const string outputFileName = "ScreenCapture.png";

                // Create DXGI Factory1
                var factory = new Factory1();
                var adapter = factory.GetAdapter1(numAdapter);

                // Create device from Adapter
                device = new SharpDX.Direct3D11.Device(adapter);


                // Get DXGI.Output
                var output = adapter.GetOutput(numOutput);
                var output1 = output.QueryInterface<Output1>();

                // Width/Height of desktop to capture
                int width = ((SharpDX.Rectangle)output.Description.DesktopBounds).Width;
                int height = ((SharpDX.Rectangle)output.Description.DesktopBounds).Height;

                // Create Staging texture CPU-accessible
                var textureDesc = new Texture2DDescription
                {
                    CpuAccessFlags = CpuAccessFlags.Read,
                    BindFlags = BindFlags.None,
                    Format = Format.B8G8R8A8_UNorm,
                    Width = width,
                    Height = height,
                    OptionFlags = ResourceOptionFlags.None,
                    MipLevels = 1,
                    ArraySize = 1,
                    SampleDescription = { Count = 1, Quality = 0 },
                    Usage = ResourceUsage.Staging
                };
                screenTexture = new Texture2D(device, textureDesc);

                // Duplicate the output
                duplicatedOutput = output1.DuplicateOutput(device);
            }
            catch (SharpDXException e)
            {
                if (device != null) device.Dispose();
                if (screenTexture != null) screenTexture.Dispose();
                if (duplicatedOutput != null) duplicatedOutput.Dispose();
                device = null;
                if (e.ResultCode.Code == SharpDX.DXGI.ResultCode.AccessDenied.Result.Code)
                {
                    device = null;
                }
                //else throw e;
            }
            catch(Exception e)
            {
                throw e;
            }


            // TODO: We should cleanp up all allocated COM objects here
        }
        public void Dispose()
        {
            if(duplicatedOutput != null) duplicatedOutput.Dispose();
            if(screenTexture != null) screenTexture.Dispose();
            if(device != null) device.Dispose();
        }

        public Bitmap getScreenDX()
        {
            if (System.Windows.Forms.Screen.AllScreens[0].Bounds.Width != LEDSetup.SCREEN_W) return null;
            try
            {
                SharpDX.DXGI.Resource screenResource;
                OutputDuplicateFrameInformation duplicateFrameInformation;

                // Try to get duplicated frame within given time
                duplicatedOutput.AcquireNextFrame(1000, out duplicateFrameInformation, out screenResource);

                // copy resource into memory that can be accessed by the CPU
                using (var screenTexture2D = screenResource.QueryInterface<Texture2D>())
                    device.ImmediateContext.CopyResource(screenTexture2D, screenTexture);

                // Get the desktop capture texture
                var mapSource = device.ImmediateContext.MapSubresource(screenTexture, 0, MapMode.Read, SharpDX.Direct3D11.MapFlags.None);

                // Create Drawing.Bitmap
                var bitmap = new System.Drawing.Bitmap(LEDSetup.SCREEN_W, LEDSetup.SCREEN_H, PixelFormat.Format32bppArgb);
                var boundsRect = new System.Drawing.Rectangle(0, 0, LEDSetup.SCREEN_W, LEDSetup.SCREEN_H);

                // Copy pixels from screen capture Texture to GDI bitmap
                var mapDest = bitmap.LockBits(boundsRect, ImageLockMode.WriteOnly, bitmap.PixelFormat);
                var sourcePtr = mapSource.DataPointer;
                var destPtr = mapDest.Scan0;
                for (int y = 0; y < LEDSetup.SCREEN_H; y++)
                {
                    // Copy a single line 
                    Utilities.CopyMemory(destPtr, sourcePtr, LEDSetup.SCREEN_W * 4);

                    // Advance pointers
                    sourcePtr = IntPtr.Add(sourcePtr, mapSource.RowPitch);
                    destPtr = IntPtr.Add(destPtr, mapDest.Stride);
                }

                // Release source and dest locks
                bitmap.UnlockBits(mapDest);
                device.ImmediateContext.UnmapSubresource(screenTexture, 0);

                // Save the output
                //bitmap.Save(outputFileName, ImageFormat.Png);

                // Capture done

                screenResource.Dispose();
                duplicatedOutput.ReleaseFrame();

                return bitmap;
            }
            catch (SharpDXException e)
            {
                if (e.ResultCode.Code != SharpDX.DXGI.ResultCode.WaitTimeout.Result.Code && e.ResultCode != SharpDX.DXGI.ResultCode.InvalidCall.Result.Code)
                {
                    if (e.ResultCode.Code == SharpDX.DXGI.ResultCode.AccessLost.Result.Code)
                    {
                        Logger.QueueLine("Device access lost, reinitializing duplication");
                        setupDX();
                        System.Threading.Thread.Sleep(200);
                    }
                    else if (e.ResultCode.Code == SharpDX.DXGI.ResultCode.DeviceRemoved.Code)
                    {
                        Logger.QueueLine("Device removed, reinitializing duplication");
                        setupDX();
                        System.Threading.Thread.Sleep(200);
                    }
                    else
                        throw e;
                }
            }
            catch (Exception e)
            {

            }
            return null;
        }
        public void draw()
        {
            if (device == null) return;
            byte[] serialData = LEDSetup.getMagicHeader();

            //Bitmap screenBitmap = getScreenGDI();
            Bitmap screenBitmap = getScreenDX();
            if (screenBitmap == null) return;

#if true
            for (int i = 0; i < LEDSetup.LED_C; i++)
            {
                int r = 0, g = 0, b = 0, c = 0;
                long re = 0, gr = 0, bl = 0;
                for (int x = (int)LEDSetup.leds[i].Coords.Left; x < (int)LEDSetup.leds[i].Coords.Right; x += 10)
                {
                    for (int y = (int)LEDSetup.leds[i].Coords.Top; y < (int)LEDSetup.leds[i].Coords.Bottom; y += 10)
                    {
                        System.Drawing.Color col;
                        lock (screenBitmap)
                        {
                            col = screenBitmap.GetPixel(x, y);
                        }
                        //if (false)
                        //{
                        //    re += (long)col.R;
                        //    gr += (long)col.G;
                        //    bl += (long)col.B;
                        //}
                        //else
                        //{
                            re += (long)(col.R * col.R);
                            gr += (long)(col.G * col.G);
                            bl += (long)(col.B * col.B);
                        //}
                        c++;
                    }
                }
                //if (false)
                //{
                //    r = (int)(re / c);
                //    g = (int)(gr / c);
                //    b = (int)(bl / c);
                //}
                //else
                //{
                    r = (int)Math.Sqrt(re / c);
                    g = (int)Math.Sqrt(gr / c);
                    b = (int)Math.Sqrt(bl / c);
                //}
                if (r > 255) r = 255;
                if (g > 255) g = 255;
                if (b > 255) b = 255;

                LEDSetup.processColor(i, serialData, r, g, b);
            }
#else
            Parallel.For(0, LEDSetup.LED_C, (i) =>
            {
                int r = 0, g = 0, b = 0, c = 0;
                long re = 0, gr = 0, bl = 0;
                for (int x = (int)LEDSetup.leds[i].Coords.Left; x < (int)LEDSetup.leds[i].Coords.Right; x += 10)
                {
                    for (int y = (int)LEDSetup.leds[i].Coords.Top; y < (int)LEDSetup.leds[i].Coords.Bottom; y += 10)
                    {
                        System.Drawing.Color col;
                        lock (screenBitmap)
                        {
                            col = screenBitmap.GetPixel(x, y);
                        }
                        if (false)
                        {
                            re += (long)col.R;
                            gr += (long)col.G;
                            bl += (long)col.B;
                        }
                        else
                        {
                            re += (long)(col.R * col.R);
                            gr += (long)(col.G * col.G);
                            bl += (long)(col.B * col.B);
                        }
                        c++;
                    }
                }
                if (false)
                {
                    r = (int)(re / c);
                    g = (int)(gr / c);
                    b = (int)(bl / c);
                }
                else
                {
                    r = (int)Math.Sqrt(re / c);
                    g = (int)Math.Sqrt(gr / c);
                    b = (int)Math.Sqrt(bl / c);
                }
                if (r > 255) r = 255;
                if (g > 255) g = 255;
                if (b > 255) b = 255;

                LEDSetup.processColor(i, serialData, r, g, b);
            });
#endif
            screenBitmap.Dispose();
            LEDSetup.sendSerialData(serialData);
        }
    }
}
