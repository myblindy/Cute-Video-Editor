using CuteVideoEditor.Core.Helpers;
using System.Collections.ObjectModel;

namespace Cute_Video_Editor.VmTests;

[TestClass]
public class ObservableTests
{
    [TestMethod]
    public void KeepInSync()
    {
        ObservableCollection<int> src = [1, 2, 3];
        ObservableCollection<string> dst = [];
        dst.KeepInSync(src, i => i.ToString());

        CollectionAssert.AreEqual((string[])["1", "2", "3"], dst);

        src.Add(4);
        CollectionAssert.AreEqual((string[])["1", "2", "3", "4"], dst);

        src.RemoveAt(1);
        CollectionAssert.AreEqual((string[])["1", "3", "4"], dst);

        src.Insert(2, 50);
        CollectionAssert.AreEqual((string[])["1", "3", "50", "4"], dst);

        src.Clear();
        CollectionAssert.AreEqual(Array.Empty<string>(), dst);
    }
}