using CuteVideoEditor.Core.Models;
using Windows.Foundation;

namespace CuteVideoEditor.Helpers;
static class ModelExtensions
{
    public static Size ToWFSize(this SizeModel size) =>
        new(size.Width, size.Height);
}
