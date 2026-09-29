using System.Threading.Tasks;
using Microsoft.Maui.Storage;

namespace EasyReader.Services
{
    public class MediaService_MAUI : IMediaService
    {
        public Task InitializeAsync() => Task.CompletedTask;

        public bool IsCameraAvailable => true;

        public bool IsTakePhotoSupported => true;

        public async Task<FileResult?> TakePhotoAsync()
        {
            try
            {
                return await MediaPicker.CapturePhotoAsync();
            }
            catch
            {
                return null;
            }
        }

        public async Task<FileResult?> PickPhotoAsync()
        {
            try
            {
                return await MediaPicker.PickPhotoAsync();
            }
            catch
            {
                return null;
            }
        }

        public async Task<FileResult?> PickFileAsync()
        {
            try
            {
                return await FilePicker.PickAsync();
            }
            catch
            {
                return null;
            }
        }
    }
}
