using AssestBundleTools.Presentation.WinUI.Services;

namespace AssestBundleTools.Presentation.Tests.Services.Updates;

public sealed class ApplicationUpdateReleaseNotesFormatterTests
{
    [Fact]
    public void Format_ShouldUseLocalizedFallback_WhenNotesAreMissing()
    {
        var formatted = ApplicationUpdateReleaseNotesFormatter.Format(null, "暂无更新说明");

        Assert.Equal("暂无更新说明", formatted);
    }

    [Fact]
    public void Format_ShouldPresentCommonMarkdownAsReadableSelectableText()
    {
        const string notes = "# 版本 0.1.1\r\n\r\n## 更新内容\r\n- 修复高 DPI 布局\r\n* 改进更新提示\u0001";

        var formatted = ApplicationUpdateReleaseNotesFormatter.Format(notes, "暂无更新说明");

        Assert.Equal(
            string.Join(
                Environment.NewLine,
                "版本 0.1.1",
                string.Empty,
                "更新内容",
                "• 修复高 DPI 布局",
                "• 改进更新提示"),
            formatted);
    }

    [Fact]
    public void Format_ShouldBoundRemoteContentByLinesAndCharacters()
    {
        var manyLines = string.Join('\n', Enumerable.Range(1, 80).Select(index => $"- 条目 {index}"));
        var longLine = new string('a', ApplicationUpdateReleaseNotesFormatter.MaximumCharacterCount + 200);

        var formattedLines = ApplicationUpdateReleaseNotesFormatter.Format(manyLines, "empty");
        var formattedCharacters = ApplicationUpdateReleaseNotesFormatter.Format(longLine, "empty");

        Assert.Equal(ApplicationUpdateReleaseNotesFormatter.MaximumLineCount, formattedLines.Split(Environment.NewLine).Length);
        Assert.Equal(ApplicationUpdateReleaseNotesFormatter.MaximumCharacterCount, formattedCharacters.Length);
        Assert.EndsWith("…", formattedCharacters, StringComparison.Ordinal);
    }
}
