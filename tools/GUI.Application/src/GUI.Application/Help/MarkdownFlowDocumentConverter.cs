using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using WpfBlock = System.Windows.Documents.Block;
using WpfInline = System.Windows.Documents.Inline;
using WpfParagraph = System.Windows.Documents.Paragraph;
using WpfBold = System.Windows.Documents.Bold;
using WpfItalic = System.Windows.Documents.Italic;
using WpfRun = System.Windows.Documents.Run;
using WpfLineBreak = System.Windows.Documents.LineBreak;
using WpfFlowDocument = System.Windows.Documents.FlowDocument;

namespace XrayDetector.Gui.Help;

/// <summary>
/// Converts Markdown content to WPF FlowDocument (SPEC-HELP-001 Wave 2).
/// Uses Markdig for parsing with Tables, EmphasisExtras, TaskLists extensions.
/// Headings → Bold paragraphs with larger font
/// Code blocks → Paragraph with Consolas font, gray background
/// Regular text → Paragraph
/// Tables → text representation with Consolas font
/// </summary>
public class MarkdownFlowDocumentConverter
{
    private static readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UsePipeTables()
        .UseEmphasisExtras()
        .UseTaskLists()
        .Build();

    // Cached shared resources to avoid repeated allocations per block
    private static readonly FontFamily _consolasFont = new("Consolas");
    private static readonly SolidColorBrush _codeBackground;

    static MarkdownFlowDocumentConverter()
    {
        _codeBackground = new SolidColorBrush(Color.FromRgb(240, 240, 240));
        _codeBackground.Freeze(); // Thread-safe for cross-element sharing
    }

    /// <summary>
    /// Converts the given Markdown string to a FlowDocument.
    /// Returns an empty FlowDocument for null or empty input.
    /// </summary>
    public WpfFlowDocument Convert(string? markdown)
    {
        var doc = new WpfFlowDocument
        {
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 13,
            PagePadding = new Thickness(12)
        };

        if (string.IsNullOrWhiteSpace(markdown))
            return doc;

        var parsed = Markdown.Parse(markdown, _pipeline);
        foreach (var block in parsed)
        {
            var wpfBlock = ConvertBlock(block);
            if (wpfBlock != null)
                doc.Blocks.Add(wpfBlock);
        }

        return doc;
    }

    /// <summary>
    /// Asynchronously converts Markdown to FlowDocument.
    /// Runs CPU-bound Markdown.Parse() on a background thread,
    /// then creates WPF objects on the calling (UI) thread.
    /// </summary>
    public async Task<WpfFlowDocument> ConvertAsync(string? markdown)
    {
        var doc = new WpfFlowDocument
        {
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 13,
            PagePadding = new Thickness(12)
        };

        if (string.IsNullOrWhiteSpace(markdown))
            return doc;

        // Offload CPU-bound parsing to thread pool; Markdig has no WPF dependency
        var parsed = await Task.Run(() => Markdown.Parse(markdown, _pipeline));

        // WPF DependencyObjects must be created on the UI thread (continuation runs here)
        foreach (var block in parsed)
        {
            var wpfBlock = ConvertBlock(block);
            if (wpfBlock != null)
                doc.Blocks.Add(wpfBlock);
        }

        return doc;
    }

    private static WpfBlock? ConvertBlock(Markdig.Syntax.Block block)
    {
        return block switch
        {
            HeadingBlock heading => ConvertHeading(heading),
            FencedCodeBlock codeBlock => ConvertCodeBlock(codeBlock),
            CodeBlock codeBlock => ConvertCodeBlock(codeBlock),
            Markdig.Extensions.Tables.Table table => ConvertTable(table),
            ParagraphBlock paragraph => ConvertParagraph(paragraph),
            ListBlock list => ConvertList(list),
            ThematicBreakBlock => new WpfParagraph(new WpfRun("────────────────────────────────")),
            _ => ConvertFallback(block),
        };
    }

    private static WpfParagraph ConvertHeading(HeadingBlock heading)
    {
        var para = new WpfParagraph
        {
            FontWeight = FontWeights.Bold,
            FontSize = heading.Level switch
            {
                1 => 20,
                2 => 17,
                3 => 15,
                _ => 14
            },
            Margin = new Thickness(0, 8, 0, 4)
        };

        if (heading.Inline != null)
            para.Inlines.AddRange(ConvertInlines(heading.Inline));

        return para;
    }

    private static WpfParagraph ConvertCodeBlock(LeafBlock codeBlock)
    {
        var code = codeBlock.Lines.ToString() ?? string.Empty;
        var para = new WpfParagraph(new WpfRun(code))
        {
            FontFamily = _consolasFont,
            FontSize = 12,
            Background = _codeBackground,
            Padding = new Thickness(8),
            Margin = new Thickness(0, 4, 0, 4)
        };
        return para;
    }

    private static WpfParagraph ConvertTable(Markdig.Extensions.Tables.Table table)
    {
        // Simple text representation for WPF FlowDocument (tables are complex)
        var sb = new System.Text.StringBuilder();
        foreach (var row in table.OfType<Markdig.Extensions.Tables.TableRow>())
        {
            var cells = row.OfType<Markdig.Extensions.Tables.TableCell>().ToList();
            sb.AppendLine(string.Join(" | ", cells.Select(c =>
            {
                var paragraph = c.OfType<ParagraphBlock>().FirstOrDefault();
                return paragraph?.Inline != null
                    ? string.Concat(paragraph.Inline.Select(i => GetInlineText(i)))
                    : string.Empty;
            })));
        }

        return new WpfParagraph(new WpfRun(sb.ToString().TrimEnd()))
        {
            FontFamily = _consolasFont,
            FontSize = 12,
            Margin = new Thickness(0, 4, 0, 4)
        };
    }

    private static WpfParagraph ConvertParagraph(ParagraphBlock paragraph)
    {
        var para = new WpfParagraph { Margin = new Thickness(0, 2, 0, 2) };
        if (paragraph.Inline != null)
            para.Inlines.AddRange(ConvertInlines(paragraph.Inline));
        return para;
    }

    private static WpfParagraph ConvertList(ListBlock list)
    {
        var sb = new System.Text.StringBuilder();
        int index = 1;
        foreach (var item in list.OfType<ListItemBlock>())
        {
            var prefix = list.IsOrdered ? $"{index++}. " : "• ";
            foreach (var block in item.OfType<ParagraphBlock>())
            {
                if (block.Inline != null)
                    sb.AppendLine(prefix + string.Concat(block.Inline.Select(i => GetInlineText(i))));
            }
        }
        return new WpfParagraph(new WpfRun(sb.ToString().TrimEnd()));
    }

    private static WpfParagraph? ConvertFallback(Markdig.Syntax.Block block)
    {
        var text = block.ToString();
        if (string.IsNullOrWhiteSpace(text))
            return null;
        return new WpfParagraph(new WpfRun(text));
    }

    private static IEnumerable<WpfInline> ConvertInlines(ContainerInline container)
    {
        foreach (var inline in container)
        {
            yield return ConvertInline(inline);
        }
    }

    private static WpfInline ConvertInline(Markdig.Syntax.Inlines.Inline inline)
    {
        return inline switch
        {
            LiteralInline literal => new WpfRun(literal.Content.ToString()),
            EmphasisInline emphasis when emphasis.DelimiterCount == 2
                => new WpfBold(new WpfRun(string.Concat(emphasis.Select(i => GetInlineText(i))))),
            EmphasisInline emphasis
                => new WpfItalic(new WpfRun(string.Concat(emphasis.Select(i => GetInlineText(i))))),
            CodeInline code => new WpfRun(code.Content)
            {
                FontFamily = _consolasFont,
                Background = _codeBackground
            },
            LinkInline link => new WpfRun($"[{string.Concat(link.Select(i => GetInlineText(i)))}]({link.Url})"),
            LineBreakInline => new WpfLineBreak(),
            _ => new WpfRun(GetInlineText(inline))
        };
    }

    private static string GetInlineText(Markdig.Syntax.Inlines.Inline inline)
    {
        return inline switch
        {
            LiteralInline literal => literal.Content.ToString(),
            CodeInline code => code.Content,
            EmphasisInline emphasis => string.Concat(emphasis.Select(i => GetInlineText(i))),
            LinkInline link => string.Concat(link.Select(i => GetInlineText(i))),
            _ => string.Empty
        };
    }
}
