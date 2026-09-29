using System.Threading.Tasks;

namespace EasyReader.Services
{
    public interface IExternalMaps
    {
        Task NavigateTo(string name, double latitude, double longitude);
    }
}
