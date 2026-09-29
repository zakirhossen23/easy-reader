using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace SignaturePad.Forms
{
    public enum SignatureImageFormat { Jpeg, Png }

    public class SignaturePadView : View
    {
        public IEnumerable<PointF> Points => new List<PointF>();

        public Task<Stream> GetImageStreamAsync(SignatureImageFormat format, Color strokeColor = null, Color fillColor = null)
        {
            // Return an empty stream as a stub; real implementation should come from a proper SignaturePad package.
            return Task.FromResult<Stream>(new MemoryStream());
        }

        public void Clear()
        {
            // no-op stub
        }
    }
}
