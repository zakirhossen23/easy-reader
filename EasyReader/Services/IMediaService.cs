using System.Threading.Tasks;
using Microsoft.Maui.Storage;

namespace EasyReader.Services
{
    public interface IMediaService
    {
        Task InitializeAsync();
        bool IsCameraAvailable { get; }
        bool IsTakePhotoSupported { get; }
        Task<FileResult?> TakePhotoAsync();
        Task<FileResult?> PickPhotoAsync();
        Task<FileResult?> PickFileAsync();
    }
}
