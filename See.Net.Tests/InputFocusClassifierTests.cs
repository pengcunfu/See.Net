using See.Net.Core;

namespace See.Net.Tests;

public sealed class InputFocusClassifierTests
{
    [Fact]
    public void Explorer_Window_Classes_Are_Recognized()
    {
        Assert.True(InputFocusClassifier.IsExplorerClass("CabinetWClass"));
        Assert.True(InputFocusClassifier.IsExplorerClass("ExploreWClass"));
        Assert.False(InputFocusClassifier.IsExplorerClass(null));
        Assert.False(InputFocusClassifier.IsExplorerClass("Notepad"));
    }

    [Theory]
    [InlineData("Edit")]
    [InlineData("RichEdit20W")]
    [InlineData("ComboBox")]
    [InlineData("Scintilla")]
    [InlineData("Chrome_WidgetWin_1")]
    [InlineData("TEXTBOX")]
    public void Input_Controls_Are_Detected(string className)
    {
        Assert.True(InputFocusClassifier.IsLikelyInputControl(className));
    }

    [Theory]
    [InlineData("SysListView32")]
    [InlineData("DirectUIHWND")]
    [InlineData("")]
    [InlineData(null)]
    public void Non_Input_Controls_Are_Not_Detected(string? className)
    {
        Assert.False(InputFocusClassifier.IsLikelyInputControl(className));
    }
}
