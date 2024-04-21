using Cute_Video_Editor.VmTests.Helpers;
using CuteVideoEditor.ViewModels;
using System.Runtime.Intrinsics.X86;

namespace Cute_Video_Editor.VmTests;

[TestClass]
public class TrimmingMarkerTests
{
    static MainViewModel CreateDefaultTestViewModel()
    {
        var vm = Support.CreateViewModel();
        vm.MediaDuration = TimeSpan.FromMinutes(2);
        vm.MediaFrameRate = 30;
        vm.MediaPixelSize = new(1920, 1080);
        vm.VideoPlayerPixelSize = new(500, 500);
        return vm;
    }

    [TestMethod]
    public void NoTrimming()
    {
        var vm = CreateDefaultTestViewModel();
        Assert.AreEqual(vm.MediaDuration, vm.OutputMediaDuration);

        Assert.AreEqual(TimeSpan.Zero, vm.InputMediaPosition);
        Assert.AreEqual(TimeSpan.Zero, vm.OutputMediaPosition);

        vm.InputMediaPosition = TimeSpan.FromSeconds(5);
        Assert.AreEqual(TimeSpan.FromSeconds(5), vm.OutputMediaPosition);
    }

    [TestMethod]
    public void AllTrimmed()
    {
        var vm = CreateDefaultTestViewModel();
        vm.TrimmingMarkers[0].TrimAfter = true;

        Assert.AreEqual(TimeSpan.Zero, vm.InputMediaPosition);
        Assert.AreEqual(TimeSpan.Zero, vm.OutputMediaPosition);
        Assert.AreEqual(TimeSpan.Zero, vm.OutputMediaDuration);

        vm.InputMediaPosition = TimeSpan.FromSeconds(5);
        Assert.AreEqual(TimeSpan.Zero, vm.OutputMediaPosition);
    }

    [TestMethod]
    public void AddMarker()
    {
        var vm = CreateDefaultTestViewModel();
        Assert.AreEqual(1, vm.TrimmingMarkers.Count);

        vm.AddMarkerCommand.Execute(null);
        Assert.AreEqual(1, vm.TrimmingMarkers.Count);

        vm.InputMediaPosition = TimeSpan.FromSeconds(10);
        vm.AddMarkerCommand.Execute(null);
        Assert.AreEqual(2, vm.TrimmingMarkers.Count);

        vm.InputMediaPosition = TimeSpan.FromSeconds(5);
        vm.AddMarkerCommand.Execute(null);
        Assert.AreEqual(3, vm.TrimmingMarkers.Count);

        // sorted?
        CollectionAssert.AreEqual(vm.TrimmingMarkers.OrderBy(w => w.FrameNumber).ToList(), vm.TrimmingMarkers);
    }

    [TestMethod]
    public void AllTrimmed2()
    {
        var vm = CreateDefaultTestViewModel();
        vm.TrimmingMarkers[0].TrimAfter = true;

        vm.InputMediaPosition = TimeSpan.FromSeconds(10);
        vm.AddMarkerCommand.Execute(null);
        vm.TrimmingMarkers[1].TrimAfter = true;
        Assert.AreEqual(TimeSpan.Zero, vm.OutputMediaDuration);

        vm.InputMediaPosition = TimeSpan.Zero;
        Assert.AreEqual(TimeSpan.Zero, vm.OutputMediaPosition);

        vm.InputMediaPosition = TimeSpan.FromSeconds(5);
        Assert.AreEqual(TimeSpan.Zero, vm.OutputMediaPosition);

        vm.InputMediaPosition = TimeSpan.FromSeconds(10);
        Assert.AreEqual(TimeSpan.Zero, vm.OutputMediaPosition);

        vm.InputMediaPosition = TimeSpan.FromSeconds(15);
        Assert.AreEqual(TimeSpan.Zero, vm.OutputMediaPosition);
    }

    [TestMethod]
    public void ComplexTrimming()
    {
        var vm = CreateDefaultTestViewModel();

        vm.InputMediaPosition = TimeSpan.FromSeconds(5);
        vm.AddMarkerCommand.Execute(null);
        vm.InputMediaPosition = TimeSpan.FromSeconds(10);
        vm.AddMarkerCommand.Execute(null);
        vm.InputMediaPosition = TimeSpan.FromSeconds(15);
        vm.AddMarkerCommand.Execute(null);

        void AssertTrimming((TimeSpan input, TimeSpan output)[] values)
        {
            foreach (var (input, output) in values)
            {
                vm.InputMediaPosition = input;
                Assert.AreEqual(output, vm.OutputMediaPosition);
            }
        }

        AssertTrimming([
            (TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(0)),
            (TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5)),
            (TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10)),
            (TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(15)),
            (TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(20)),
            (TimeSpan.FromSeconds(25), TimeSpan.FromSeconds(25)),
        ]);

        vm.TrimmingMarkers[0].TrimAfter = true;
        AssertTrimming([
            (TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(0)),
            (TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(0)),
            (TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(5)),
            (TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(10)),
            (TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(15)),
            (TimeSpan.FromSeconds(25), TimeSpan.FromSeconds(20)),
        ]);

        vm.TrimmingMarkers[1].TrimAfter = true;
        AssertTrimming([
            (TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(0)),
            (TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(0)),
            (TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(0)),
            (TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(5)),
            (TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(10)),
            (TimeSpan.FromSeconds(25), TimeSpan.FromSeconds(15)),
        ]);

        vm.TrimmingMarkers[0].TrimAfter = false;
        AssertTrimming([
            (TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(0)),
            (TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5)),
            (TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(5)),
            (TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(10)),
            (TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(15)),
            (TimeSpan.FromSeconds(25), TimeSpan.FromSeconds(20)),
        ]);
    }
}