using Cute_Video_Editor.VmTests.Helpers;
using CuteVideoEditor.ViewModels;
using System.ComponentModel;
using System.Reflection;
using Windows.ApplicationModel.VoiceCommands;

namespace Cute_Video_Editor.VmTests;

[TestClass]
public class TrimmingMarkerTests
{
    static VideoEditorViewModel CreateDefaultTestViewModel()
    {
        var vm = Support.CreateViewModel();
        var vpvmType = vm.VideoPlayerViewModel.GetType();
        vpvmType.GetProperty(nameof(vm.VideoPlayerViewModel.InputMediaDuration))!.SetMethod!.Invoke(vm.VideoPlayerViewModel, [TimeSpan.FromMinutes(2)]);
        vpvmType.GetProperty(nameof(vm.VideoPlayerViewModel.MediaFrameRate))!.SetMethod!.Invoke(vm.VideoPlayerViewModel, [30]);

        // trigger a trimming marker rebuild
        vm.GetType().GetMethod("RebuildTrimmingMarkers", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(vm, []);

        vm.MediaPixelSize = new(1920, 1080);
        vm.VideoPlayerPixelSize = new(500, 500);
        return vm;
    }

    [TestMethod]
    public void NoTrimming()
    {
        var vm = CreateDefaultTestViewModel();
        Assert.AreEqual(vm.VideoPlayerViewModel.InputMediaDuration, vm.VideoPlayerViewModel.OutputMediaDuration);

        Assert.AreEqual(TimeSpan.Zero, vm.VideoPlayerViewModel.InputMediaPosition);
        Assert.AreEqual(TimeSpan.Zero, vm.VideoPlayerViewModel.OutputMediaPosition);

        vm.VideoPlayerViewModel.InputMediaPosition = TimeSpan.FromSeconds(5);
        Assert.AreEqual(TimeSpan.FromSeconds(5), vm.VideoPlayerViewModel.OutputMediaPosition);
    }

    [TestMethod]
    public void AllTrimmed()
    {
        var vm = CreateDefaultTestViewModel();
        vm.VideoPlayerViewModel.TrimmingMarkers[0].TrimAfter = true;

        Assert.AreEqual(TimeSpan.Zero, vm.VideoPlayerViewModel.InputMediaPosition);
        Assert.AreEqual(TimeSpan.Zero, vm.VideoPlayerViewModel.OutputMediaPosition);
        Assert.AreEqual(TimeSpan.Zero, vm.VideoPlayerViewModel.OutputMediaDuration);

        vm.VideoPlayerViewModel.InputMediaPosition = TimeSpan.FromSeconds(5);
        Assert.AreEqual(TimeSpan.Zero, vm.VideoPlayerViewModel.OutputMediaPosition);
    }

    [TestMethod]
    public void AddMarker()
    {
        var vm = CreateDefaultTestViewModel();
        Assert.AreEqual(1, vm.VideoPlayerViewModel.TrimmingMarkers.Count);

        vm.AddMarkerCommand.Execute(null);
        Assert.AreEqual(1, vm.VideoPlayerViewModel.TrimmingMarkers.Count);

        vm.VideoPlayerViewModel.InputMediaPosition = TimeSpan.FromSeconds(10);
        vm.AddMarkerCommand.Execute(null);
        Assert.AreEqual(2, vm.VideoPlayerViewModel.TrimmingMarkers.Count);

        vm.VideoPlayerViewModel.InputMediaPosition = TimeSpan.FromSeconds(5);
        vm.AddMarkerCommand.Execute(null);
        Assert.AreEqual(3, vm.VideoPlayerViewModel.TrimmingMarkers.Count);

        // sorted?
        CollectionAssert.AreEqual(vm.VideoPlayerViewModel.TrimmingMarkers.OrderBy(w => w.FrameNumber).ToList(), vm.VideoPlayerViewModel.TrimmingMarkers);
    }

    [TestMethod]
    public void AllTrimmed2()
    {
        var vm = CreateDefaultTestViewModel();
        vm.VideoPlayerViewModel.TrimmingMarkers[0].TrimAfter = true;

        vm.VideoPlayerViewModel.InputMediaPosition = TimeSpan.FromSeconds(10);
        vm.AddMarkerCommand.Execute(null);
        vm.VideoPlayerViewModel.TrimmingMarkers[1].TrimAfter = true;
        Assert.AreEqual(TimeSpan.Zero, vm.VideoPlayerViewModel.OutputMediaDuration);

        vm.VideoPlayerViewModel.InputMediaPosition = TimeSpan.Zero;
        Assert.AreEqual(TimeSpan.Zero, vm.VideoPlayerViewModel.OutputMediaPosition);

        vm.VideoPlayerViewModel.InputMediaPosition = TimeSpan.FromSeconds(5);
        Assert.AreEqual(TimeSpan.Zero, vm.VideoPlayerViewModel.OutputMediaPosition);

        vm.VideoPlayerViewModel.InputMediaPosition = TimeSpan.FromSeconds(10);
        Assert.AreEqual(TimeSpan.Zero, vm.VideoPlayerViewModel.OutputMediaPosition);

        vm.VideoPlayerViewModel.InputMediaPosition = TimeSpan.FromSeconds(15);
        Assert.AreEqual(TimeSpan.Zero, vm.VideoPlayerViewModel.OutputMediaPosition);
    }

    [TestMethod]
    public void ComplexTrimming()
    {
        var vm = CreateDefaultTestViewModel();

        vm.VideoPlayerViewModel.InputMediaPosition = TimeSpan.FromSeconds(5);
        vm.AddMarkerCommand.Execute(null);
        vm.VideoPlayerViewModel.InputMediaPosition = TimeSpan.FromSeconds(10);
        vm.AddMarkerCommand.Execute(null);
        vm.VideoPlayerViewModel.InputMediaPosition = TimeSpan.FromSeconds(15);
        vm.AddMarkerCommand.Execute(null);

        void AssertTrimming((TimeSpan input, TimeSpan output)[] values)
        {
            foreach (var (input, output) in values)
            {
                vm.VideoPlayerViewModel.InputMediaPosition = input;
                Assert.AreEqual(output, vm.VideoPlayerViewModel.OutputMediaPosition);
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

        vm.VideoPlayerViewModel.TrimmingMarkers[0].TrimAfter = true;
        AssertTrimming([
            (TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(0)),
            (TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(0)),
            (TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(5)),
            (TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(10)),
            (TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(15)),
            (TimeSpan.FromSeconds(25), TimeSpan.FromSeconds(20)),
        ]);

        vm.VideoPlayerViewModel.TrimmingMarkers[1].TrimAfter = true;
        AssertTrimming([
            (TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(0)),
            (TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(0)),
            (TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(0)),
            (TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(5)),
            (TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(10)),
            (TimeSpan.FromSeconds(25), TimeSpan.FromSeconds(15)),
        ]);

        vm.VideoPlayerViewModel.TrimmingMarkers[0].TrimAfter = false;
        AssertTrimming([
            (TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(0)),
            (TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5)),
            (TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(5)),
            (TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(10)),
            (TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(15)),
            (TimeSpan.FromSeconds(25), TimeSpan.FromSeconds(20)),
        ]);
    }

    [TestMethod]
    public void SetOutputMediaPosition()
    {
        var vm = CreateDefaultTestViewModel();

        void AssertOutputMediaPositions((TimeSpan output, TimeSpan input)[] vals)
        {
            foreach (var (output, input) in vals)
            {
                vm.VideoPlayerViewModel.OutputMediaPosition = output;
                Assert.AreEqual(input, vm.VideoPlayerViewModel.InputMediaPosition);
            }
        }

        AssertOutputMediaPositions([
            (TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(0)),
            (TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5)),
            (TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10)),
            (TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(15)),
            (TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(20)),
            (TimeSpan.FromSeconds(25), TimeSpan.FromSeconds(25)),
        ]);

        vm.VideoPlayerViewModel.InputMediaPosition = TimeSpan.FromSeconds(5);
        vm.AddMarkerCommand.Execute(null);
        AssertOutputMediaPositions([
            (TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(0)),
            (TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5)),
            (TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10)),
            (TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(15)),
            (TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(20)),
            (TimeSpan.FromSeconds(25), TimeSpan.FromSeconds(25)),
        ]);

        vm.VideoPlayerViewModel.TrimmingMarkers[0].TrimAfter = true;
        AssertOutputMediaPositions([
            (TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(5)),
            (TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10)),
            (TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(15)),
            (TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(20)),
            (TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(25)),
            (TimeSpan.FromSeconds(25), TimeSpan.FromSeconds(30)),
        ]);
    }

    [TestMethod]
    public void TrimmingMarkers()
    {
        var vm = CreateDefaultTestViewModel();

        void AssertOutputTrimmingMarkers(TimeSpan outputMediaDuration, DisjunctTrimmingMarkerEntry[] disjunctOutputTrims, TimeSpan[] nonDisjunctOutputMarkers)
        {
            Assert.AreEqual(outputMediaDuration, vm.VideoPlayerViewModel.OutputMediaDuration, "media duration");
            CollectionAssert.AreEqual(disjunctOutputTrims, vm.DisjunctOutputTrims, "disjunct output trims");
            CollectionAssert.AreEqual(nonDisjunctOutputMarkers, vm.NonDisjunctOutputMarkers, "non-disjunct output markers");
        }

        AssertOutputTrimmingMarkers(vm.VideoPlayerViewModel.InputMediaDuration, [
                new(TimeSpan.Zero, vm.VideoPlayerViewModel.OutputMediaDuration)
            ], [
            ]);

        vm.VideoPlayerViewModel.InputMediaPosition = TimeSpan.FromSeconds(10);
        vm.AddMarkerCommand.Execute(null);
        AssertOutputTrimmingMarkers(vm.VideoPlayerViewModel.InputMediaDuration, [
                new(TimeSpan.Zero, vm.VideoPlayerViewModel.OutputMediaDuration)
            ], [
                TimeSpan.FromSeconds(10)
            ]);

        vm.VideoPlayerViewModel.InputMediaPosition = TimeSpan.FromSeconds(20);
        vm.AddMarkerCommand.Execute(null);
        AssertOutputTrimmingMarkers(vm.VideoPlayerViewModel.InputMediaDuration, [
                new(TimeSpan.Zero, vm.VideoPlayerViewModel.OutputMediaDuration)
            ], [
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(20)
            ]);

        vm.VideoPlayerViewModel.TrimmingMarkers[1].TrimAfter = true;
        AssertOutputTrimmingMarkers(vm.VideoPlayerViewModel.InputMediaDuration - TimeSpan.FromSeconds(10), [
                new(TimeSpan.Zero, TimeSpan.FromSeconds(10)),
                new(TimeSpan.FromSeconds(10), vm.VideoPlayerViewModel.OutputMediaDuration)
            ], [
            ]);

        vm.VideoPlayerViewModel.TrimmingMarkers[0].TrimAfter = true;
        AssertOutputTrimmingMarkers(vm.VideoPlayerViewModel.InputMediaDuration - TimeSpan.FromSeconds(20), [
                new(TimeSpan.Zero, vm.VideoPlayerViewModel.OutputMediaDuration)
            ], [
            ]);

        vm.VideoPlayerViewModel.TrimmingMarkers[2].TrimAfter = true;
        AssertOutputTrimmingMarkers(TimeSpan.Zero, [
                new(TimeSpan.Zero, vm.VideoPlayerViewModel.OutputMediaDuration)
            ], [
            ]);

        vm.VideoPlayerViewModel.TrimmingMarkers[0].TrimAfter = false;
        AssertOutputTrimmingMarkers(TimeSpan.FromSeconds(10), [
                new(TimeSpan.Zero, vm.VideoPlayerViewModel.OutputMediaDuration)
            ], [
            ]);

        vm.VideoPlayerViewModel.TrimmingMarkers[1].TrimAfter = false;
        AssertOutputTrimmingMarkers(TimeSpan.FromSeconds(20), [
                new(TimeSpan.Zero, vm.VideoPlayerViewModel.OutputMediaDuration)
            ], [
                TimeSpan.FromSeconds(10)
            ]);
    }

    [TestMethod]
    public void AutoCropKeyFramesOnTrimmingMidEnd()
    {
        var vm = CreateDefaultTestViewModel();

        void AssertCropKeyFrames(long[] expectedFrameNumbers) =>
            CollectionAssert.AreEqual(expectedFrameNumbers, vm.CropFrames.Select(w => w.FrameNumber).ToList());

        AssertCropKeyFrames([0]);

        vm.VideoPlayerViewModel.InputMediaPosition = TimeSpan.FromSeconds(10);
        var frameMarker1 = vm.VideoPlayerViewModel.GetFrameNumberFromPosition(vm.VideoPlayerViewModel.InputMediaPosition);
        vm.AddMarkerCommand.Execute(null);
        AssertCropKeyFrames([0]);

        vm.VideoPlayerViewModel.InputMediaPosition = TimeSpan.FromSeconds(20);
        var frameMarker2 = vm.VideoPlayerViewModel.GetFrameNumberFromPosition(vm.VideoPlayerViewModel.InputMediaPosition);
        vm.AddMarkerCommand.Execute(null);
        AssertCropKeyFrames([0]);

        vm.VideoPlayerViewModel.InputMediaPosition = TimeSpan.FromSeconds(15);
        vm.AddTrimCommand.Execute(null);
        AssertCropKeyFrames([0, frameMarker1 - 1, frameMarker1]);

        vm.VideoPlayerViewModel.InputMediaPosition = TimeSpan.FromSeconds(25);
        vm.AddTrimCommand.Execute(null);
        AssertCropKeyFrames([0, frameMarker1 - 1, frameMarker1]);
    }

    [TestMethod]
    public void AutoCropKeyFramesOnTrimmingMidStart()
    {
        var vm = CreateDefaultTestViewModel();

        void AssertCropKeyFrames(long[] expectedFrameNumbers) =>
            CollectionAssert.AreEqual(expectedFrameNumbers, vm.CropFrames.Select(w => w.FrameNumber).ToList());

        AssertCropKeyFrames([0]);

        vm.VideoPlayerViewModel.InputMediaPosition = TimeSpan.FromSeconds(10);
        var frameMarker1 = vm.VideoPlayerViewModel.GetFrameNumberFromPosition(vm.VideoPlayerViewModel.InputMediaPosition);
        vm.AddMarkerCommand.Execute(null);
        AssertCropKeyFrames([0]);

        vm.VideoPlayerViewModel.InputMediaPosition = TimeSpan.FromSeconds(20);
        var frameMarker2 = vm.VideoPlayerViewModel.GetFrameNumberFromPosition(vm.VideoPlayerViewModel.InputMediaPosition);
        vm.AddMarkerCommand.Execute(null);
        AssertCropKeyFrames([0]);

        vm.VideoPlayerViewModel.InputMediaPosition = TimeSpan.FromSeconds(15);
        vm.AddTrimCommand.Execute(null);
        AssertCropKeyFrames([0, frameMarker1 - 1, frameMarker1]);

        vm.VideoPlayerViewModel.InputMediaPosition = TimeSpan.FromSeconds(5);
        vm.AddTrimCommand.Execute(null);
        AssertCropKeyFrames([0]);
    }

    [TestMethod]
    public void AutoCropKeyFramesShrinkingOnRepeatedTrims()
    {
        var vm = CreateDefaultTestViewModel();

        void AssertCropKeyFrames(long[] expectedFrameNumbers) =>
            CollectionAssert.AreEqual(expectedFrameNumbers, vm.CropFrames.Select(w => w.FrameNumber).ToList());

        AssertCropKeyFrames([0]);

        vm.VideoPlayerViewModel.InputMediaPosition = TimeSpan.FromSeconds(10);
        var frameMarker1 = vm.VideoPlayerViewModel.GetFrameNumberFromPosition(vm.VideoPlayerViewModel.InputMediaPosition);
        vm.AddMarkerCommand.Execute(null);
        AssertCropKeyFrames([0]);

        vm.VideoPlayerViewModel.InputMediaPosition = TimeSpan.FromSeconds(20);
        var frameMarker2 = vm.VideoPlayerViewModel.GetFrameNumberFromPosition(vm.VideoPlayerViewModel.InputMediaPosition);
        vm.AddMarkerCommand.Execute(null);
        AssertCropKeyFrames([0]);

        vm.VideoPlayerViewModel.InputMediaPosition = TimeSpan.FromSeconds(30);
        var frameMarker3 = vm.VideoPlayerViewModel.GetFrameNumberFromPosition(vm.VideoPlayerViewModel.InputMediaPosition);
        vm.AddMarkerCommand.Execute(null);
        AssertCropKeyFrames([0]);

        vm.VideoPlayerViewModel.InputMediaPosition = TimeSpan.FromSeconds(35);
        vm.AddTrimCommand.Execute(null);
        AssertCropKeyFrames([0, frameMarker3 - 1, frameMarker3]);

        vm.VideoPlayerViewModel.InputMediaPosition = TimeSpan.FromSeconds(25);
        vm.AddTrimCommand.Execute(null);
        AssertCropKeyFrames([0, frameMarker2 - 1, frameMarker2]);

        vm.VideoPlayerViewModel.InputMediaPosition = TimeSpan.FromSeconds(15);
        vm.AddTrimCommand.Execute(null);
        AssertCropKeyFrames([0, frameMarker1 - 1, frameMarker1]);

        vm.VideoPlayerViewModel.InputMediaPosition = TimeSpan.FromSeconds(5);
        vm.AddTrimCommand.Execute(null);
        AssertCropKeyFrames([0]);
    }

    [TestMethod]
    public void DisjunctOutputTrims()
    {
        var vm = CreateDefaultTestViewModel();

        void AssertDisjunctOutputTrims((TimeSpan From, TimeSpan to)[] expected) =>
            CollectionAssert.AreEqual(expected, vm.DisjunctOutputTrims.Select(w => (w.From, w.To)).ToList());

        AssertDisjunctOutputTrims([(TimeSpan.Zero, vm.VideoPlayerViewModel.OutputMediaDuration)]);

        vm.VideoPlayerViewModel.InputMediaPosition = TimeSpan.FromSeconds(10);
        var frameMarker1 = vm.VideoPlayerViewModel.GetFrameNumberFromPosition(vm.VideoPlayerViewModel.InputMediaPosition);
        vm.AddMarkerCommand.Execute(null);
        AssertDisjunctOutputTrims([(TimeSpan.Zero, vm.VideoPlayerViewModel.OutputMediaDuration)]);

        vm.VideoPlayerViewModel.InputMediaPosition = TimeSpan.FromSeconds(30);
        var frameMarker3 = vm.VideoPlayerViewModel.GetFrameNumberFromPosition(vm.VideoPlayerViewModel.InputMediaPosition);
        vm.AddMarkerCommand.Execute(null);
        AssertDisjunctOutputTrims([(TimeSpan.Zero, vm.VideoPlayerViewModel.OutputMediaDuration)]);

        vm.VideoPlayerViewModel.InputMediaPosition = TimeSpan.FromSeconds(35);
        vm.AddTrimCommand.Execute(null);
        AssertDisjunctOutputTrims([(TimeSpan.Zero, TimeSpan.FromSeconds(30))]);

        vm.VideoPlayerViewModel.InputMediaPosition = TimeSpan.FromSeconds(0.5);
        vm.AddTrimCommand.Execute(null);
        AssertDisjunctOutputTrims([(TimeSpan.Zero, TimeSpan.FromSeconds(20))]);

        vm.VideoPlayerViewModel.InputMediaPosition = TimeSpan.FromSeconds(20);
        var frameMarker2 = vm.VideoPlayerViewModel.GetFrameNumberFromPosition(vm.VideoPlayerViewModel.InputMediaPosition);
        vm.AddMarkerCommand.Execute(null);
        AssertDisjunctOutputTrims([(TimeSpan.Zero, TimeSpan.FromSeconds(20))]);
    }

    [TestMethod]
    public void DisjunctOutputTrims2()
    {
        // 1. mark1 mark2 mark3
        // 2. trim mark1-mark2
        // 3. insert mark at the new beginning

        var vm = CreateDefaultTestViewModel();

        void AssertDisjunctOutputTrims((TimeSpan From, TimeSpan to)[] expected) =>
            CollectionAssert.AreEqual(expected, vm.DisjunctOutputTrims.Select(w => (w.From, w.To)).ToList());
        
        // 1.
        vm.VideoPlayerViewModel.InputMediaPosition = TimeSpan.FromSeconds(10);
        var frameMarker1 = vm.VideoPlayerViewModel.GetFrameNumberFromPosition(vm.VideoPlayerViewModel.InputMediaPosition);
        vm.AddMarkerCommand.Execute(null);
        AssertDisjunctOutputTrims([(TimeSpan.Zero, vm.VideoPlayerViewModel.OutputMediaDuration)]);

        vm.VideoPlayerViewModel.InputMediaPosition = TimeSpan.FromSeconds(20);
        var frameMarker2 = vm.VideoPlayerViewModel.GetFrameNumberFromPosition(vm.VideoPlayerViewModel.InputMediaPosition);
        vm.AddMarkerCommand.Execute(null);
        AssertDisjunctOutputTrims([(TimeSpan.Zero, vm.VideoPlayerViewModel.OutputMediaDuration)]);

        vm.VideoPlayerViewModel.InputMediaPosition = TimeSpan.FromSeconds(30);
        var frameMarker3 = vm.VideoPlayerViewModel.GetFrameNumberFromPosition(vm.VideoPlayerViewModel.InputMediaPosition);
        vm.AddMarkerCommand.Execute(null);
        AssertDisjunctOutputTrims([(TimeSpan.Zero, vm.VideoPlayerViewModel.OutputMediaDuration)]);

        // 2.
        vm.VideoPlayerViewModel.InputMediaPosition = TimeSpan.FromSeconds(15);
        vm.AddTrimCommand.Execute(null);
        AssertDisjunctOutputTrims([
            (TimeSpan.Zero, TimeSpan.FromSeconds(10)),
            (TimeSpan.FromSeconds(10), vm.VideoPlayerViewModel.InputMediaDuration - TimeSpan.FromSeconds(10))]);

        // 3.
        vm.VideoPlayerViewModel.OutputMediaPosition = TimeSpan.FromSeconds(5);
        vm.AddMarkerCommand.Execute(null);
        AssertDisjunctOutputTrims([
            (TimeSpan.Zero, TimeSpan.FromSeconds(10)),
            (TimeSpan.FromSeconds(10), vm.VideoPlayerViewModel.InputMediaDuration - TimeSpan.FromSeconds(10))]);
    }
}