using EasyReader.ViewModels;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;
using Microsoft.Maui.Graphics;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace EasyReader.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class SOClosePage : ContentPage
    {
        public SOClosePage()
        {
            InitializeComponent();
        }
        protected override void OnAppearing()
        {
            BindingContext = new SOCloseViewModel(Navigation);
            base.OnAppearing();
        }

        // SET BUTTONS BASED ON SCREEN ORIENTATION
        // left, right, top, bottom
        protected override void OnSizeAllocated(double width, double height)
        {
            base.OnSizeAllocated(width, height);
            var idiom = DeviceInfo.Idiom;

            // VERTICAL
            if (height > width)
            {
                outerGrid.ColumnDefinitions.Clear();
                outerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                // move actionGrid into UpdateActionGrid (remove from previous parent first)
                if (actionGrid.Parent is Layout oldLayout && oldLayout != UpdateActionGrid)
                {
                    oldLayout.Children.Remove(actionGrid);
                }
                if (!UpdateActionGrid.Children.Contains(actionGrid))
                    UpdateActionGrid.Children.Add(actionGrid);
                UpdateActionGrid.RowDefinitions.Clear();
                UpdateActionGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                UpdateActionGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });


                Grid.SetRow(UpdateActionGrid, 1);


                innerTimingGrid.RowDefinitions.Clear();
                innerTimingGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(180, GridUnitType.Absolute) });
                innerTimingGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });


            }
            // HORIZONTAL
            else
            {
                outerGrid.ColumnDefinitions.Clear();
                outerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                outerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                // move actionGrid into innerTimingGrid (remove from previous parent first)
                if (actionGrid.Parent is Layout oldLayout2 && oldLayout2 != innerTimingGrid)
                {
                    oldLayout2.Children.Remove(actionGrid);
                }
                if (!innerTimingGrid.Children.Contains(actionGrid))
                    innerTimingGrid.Children.Add(actionGrid);

                UpdateActionGrid.RowDefinitions.Clear();
                UpdateActionGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                Grid.SetRow(UpdateActionGrid, 0);


                innerTimingGrid.RowDefinitions.Clear();
                innerTimingGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                innerTimingGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
                innerTimingGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });


            }
        }

        // SEND SIGNATURE TO VIEW MODEL
        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();

            var vm = (SOCloseViewModel)BindingContext;
            vm.SignatureFromStream = async () => await GetSignatureBytesAsync();
        }

        // Clear signature pad when Clear button is clicked
        public void OnClearSignatureClicked(object sender, EventArgs e)
        {
            try
            {
                signaturePad?.Clear();
            }
            catch
            {
                // swallow any rare errors to avoid crashing the UI
            }
        }

        // Try to obtain signature as image bytes from the DrawingView using available APIs (reflection fallback).

        // Try to obtain signature as image bytes from the DrawingView using available APIs (reflection fallback).
        private async Task<byte[]> GetSignatureBytesAsync()
        {
            try
            {
                if (signaturePad == null)
                    return null;

                var type = signaturePad.GetType();
                System.Diagnostics.Debug.WriteLine($"[Signature] signaturePad type: {type.FullName}");

                // Try methods that return streams or byte[] (by name heuristics)
                var methods = type.GetMethods().Where(m =>
                    m.Name.IndexOf("GetImage", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    m.Name.IndexOf("GetImageStream", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    m.Name.IndexOf("GetImageAsync", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    m.Name.IndexOf("SaveImage", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    m.Name.IndexOf("GetJpeg", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    m.Name.IndexOf("Capture", StringComparison.OrdinalIgnoreCase) >= 0).ToList();

                System.Diagnostics.Debug.WriteLine($"[Signature] Candidate methods: {string.Join(", ", methods.Select(m => m.Name))}");

                foreach (var m in methods)
                {
                    try
                    {
                        // build default parameters for method
                        var parameters = m.GetParameters();
                        object[] args = null;
                        if (parameters.Length > 0)
                        {
                            args = new object[parameters.Length];
                            for (int i = 0; i < parameters.Length; i++)
                            {
                                var pType = parameters[i].ParameterType;
                                if (pType.IsEnum)
                                {
                                    var vals = Enum.GetValues(pType);
                                    args[i] = vals.Length > 0 ? vals.GetValue(0) : Activator.CreateInstance(pType);
                                }
                                else if (pType == typeof(double) || pType == typeof(float))
                                    args[i] = Convert.ChangeType(1.0, pType);
                                else if (pType == typeof(int))
                                    args[i] = 1;
                                else if (pType == typeof(bool))
                                    args[i] = true;
                                else if (pType.FullName == "System.Threading.CancellationToken")
                                    args[i] = System.Threading.CancellationToken.None;
                                else
                                {
                                    try
                                    {
                                        args[i] = Activator.CreateInstance(pType);
                                    }
                                    catch
                                    {
                                        args[i] = null;
                                    }
                                }
                            }
                        }
                        var result = m.Invoke(signaturePad, args);
                        if (result == null)
                            continue;

                        var bytes = await TryConvertResultToBytesAsync(result).ConfigureAwait(false);
                        if (bytes != null)
                            return bytes;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Signature] Error invoking {m.Name}: {ex}");
                        // ignore and try next method
                    }
                }

                // Fallback: check a variety of stroke collection property names used by different controls
                var strokePropNames = new[] { "Lines", "Strokes", "Paths", "Points", "Drawables" };
                foreach (var pname in strokePropNames)
                {
                    var prop = type.GetProperty(pname, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (prop != null)
                    {
                        var lines = prop.GetValue(signaturePad) as IEnumerable;
                        if (lines == null)
                        {
                            System.Diagnostics.Debug.WriteLine($"[Signature] {pname} property is null");
                            return null;
                        }
                        var en = lines.GetEnumerator();
                        if (!en.MoveNext())
                        {
                            System.Diagnostics.Debug.WriteLine($"[Signature] No strokes found in {pname}");
                            return null; // no strokes, no signature
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[Signature] Strokes present in {pname}");
                            break; // we have strokes, continue to attempt capture below
                        }
                    }
                }

                // Try calling MAUI's CaptureAsync on the visual element (this resolves extension/instance capture implementations).
                try
                {
                    // If CaptureAsync is available as an instance or extension, this will call it (compile-time exists on VisualElement in MAUI).
                    var captureTask = signaturePad.CaptureAsync();
                    if (captureTask != null)
                    {
                        await captureTask.ConfigureAwait(false);
                        var resProp = captureTask.GetType().GetProperty("Result");
                        var res = resProp?.GetValue(captureTask);
                        var bytes = await TryConvertResultToBytesAsync(res).ConfigureAwait(false);
                        if (bytes != null)
                            return bytes;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Signature] CaptureAsync failed: {ex}");
                }

                System.Diagnostics.Debug.WriteLine("[Signature] No suitable capture method produced image bytes");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Signature] Exception in GetSignatureBytesAsync: {ex}");
                return null;
            }
        }

        // Try to convert various image-like results to byte[]
        private static async Task<byte[]> TryConvertResultToBytesAsync(object result)
        {
            if (result == null) return null;

            // If result is Task, await and recurse
            if (result is Task task)
            {
                await task.ConfigureAwait(false);
                var resProp = task.GetType().GetProperty("Result");
                var res = resProp?.GetValue(task);
                return await TryConvertResultToBytesAsync(res).ConfigureAwait(false);
            }

            // Stream or byte[] directly
            if (result is Stream s)
                return await ImageConverter.ReadFully(s).ConfigureAwait(false);
            if (result is byte[] b)
                return b;

            // Some image types expose Save(Stream) or SaveAsync(Stream), or methods returning Stream/byte[]
            var rType = result.GetType();
            var saveToStream = rType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m =>
                {
                    var n = m.Name.ToLowerInvariant();
                    var ps = m.GetParameters();
                    return (n.Contains("save") || n.Contains("write") || n.Contains("as")) &&
                           ps.Length >= 1 && typeof(Stream).IsAssignableFrom(ps[0].ParameterType);
                });

            if (saveToStream != null)
            {
                try
                {
                    using var ms = new MemoryStream();
                    var parameters = saveToStream.GetParameters();
                    // Build parameter list: pass stream for first param, try defaults for others
                    var args = new object[parameters.Length];
                    args[0] = ms;
                    for (int i = 1; i < parameters.Length; i++)
                    {
                        var pType = parameters[i].ParameterType;
                        try
                        {
                            args[i] = pType.IsValueType ? Activator.CreateInstance(pType) : null;
                        }
                        catch
                        {
                            args[i] = null;
                        }
                    }
                    var saveRes = saveToStream.Invoke(result, args);
                    // If Save method returns a Task, await it
                    if (saveRes is Task saveTask)
                        await saveTask.ConfigureAwait(false);
                    ms.Seek(0, SeekOrigin.Begin);
                    return ms.ToArray();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Signature] saveToStream failed: {ex}");
                }
            }

            // Methods that return Stream or byte[] directly with no params
            var retNoParam = rType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m => m.GetParameters().Length == 0 &&
                                     (typeof(Stream).IsAssignableFrom(m.ReturnType) || m.ReturnType == typeof(byte[])));
            if (retNoParam != null)
            {
                try
                {
                    var r = retNoParam.Invoke(result, null);
                    if (r is Stream s2)
                        return await ImageConverter.ReadFully(s2).ConfigureAwait(false);
                    if (r is byte[] b2)
                        return b2;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Signature] retNoParam failed: {ex}");
                }
            }

            // As a last resort, try to see if object has a property exposing a stream/bytes
            var props = rType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var p in props)
            {
                try
                {
                    var val = p.GetValue(result);
                    if (val is Stream s3)
                        return await ImageConverter.ReadFully(s3).ConfigureAwait(false);
                    if (val is byte[] b3)
                        return b3;
                }
                catch { }
            }

            return null;
        }
    }

    // GET SIGNATURE IMAGE BYTES
    public static class ImageConverter
    {
        public static async Task<byte[]> ReadFully(Stream input)
        {
            byte[] buffer = new byte[16 * 1024];
            using (var ms = new MemoryStream())
            {
                int read;
                while ((read = await input.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }
                return ms.ToArray();
            }
        }
    }
}