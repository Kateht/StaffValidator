using System.Text;
using System.IO;
using System.Linq;

namespace StaffValidator.Core.Services
{
    public static class DotExporter
    {
        public static string ExportSimpleNfaToDot(SimpleNfa nfa, string name = "NFA")
        {
            var sb = new StringBuilder();
            sb.AppendLine($"digraph {name} {{");
            sb.AppendLine(" rankdir=LR;");
            sb.AppendLine(" node [shape=circle];");
            sb.AppendLine($" __start [shape=point];");
            sb.AppendLine($" __start -> \"{nfa.Start.Id}\";");

            foreach (var s in nfa.States)
            {
                var shape = s.IsAccept ? "doublecircle" : "circle";
                sb.AppendLine($" \"{s.Id}\" [shape={shape}];");
            }

            foreach (var s in nfa.States)
            {
                foreach (var kv in s.Transitions)
                {
                    var c = kv.Key;
                    var label = c switch
                    {
                        '\u0000' => "ANY_NONSPACE",
                        '\u0001' => "DIGIT",
                        _ => c.ToString()
                    };
                    foreach (var t in kv.Value)
                        sb.AppendLine($" \"{s.Id}\" -> \"{t.Id}\" [label=\"{label}\"];");
                }
                foreach (var e in s.Epsilon)
                    sb.AppendLine($" \"{s.Id}\" -> \"{e.Id}\" [label=\"ε\"];");
            }

            sb.AppendLine("}");
            return sb.ToString();
        }

        public static void WriteDotToFile(string dot, string path)
        {
            // Ensure UTF8 (no BOM) to avoid Graphviz issues
            File.WriteAllText(path, dot, new System.Text.UTF8Encoding(false));
        }
    }
}
