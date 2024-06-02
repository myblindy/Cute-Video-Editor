using CuteVideoEditor.Core.Models;
using Microsoft.UI.Xaml;
using Windows.Foundation;

namespace CuteVideoEditor.Helpers;
public static class ModelExtensions
{
    public static Size ToWFSize(this SizeModel size) =>
        new(size.Width, size.Height);

    public static Thickness ToMUIThickness(this ThicknessModel thickness) =>
        new(thickness.Left, thickness.Top, thickness.Right, thickness.Bottom);
}
