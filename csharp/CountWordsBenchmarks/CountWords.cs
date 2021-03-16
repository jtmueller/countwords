using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Microsoft.Toolkit.HighPerformance;
using Microsoft.Toolkit.HighPerformance.Buffers;

namespace CountWordsBenchmarks
{
    [Config(typeof(BenchmarkConfig))]
    public class CountWords
    {
        // https://benhoyt.com/writings/count-words/

        [Benchmark(Baseline = true)]
        public void Simple()
        {
            // Original version by John Taylor

            string line;
            while ((line = _input.ReadLine()) != null)
            {
                line = line.ToLower();
                var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                foreach (string word in words)
                {
                    _counts[word] = _counts.GetValueOrDefault(word, 0) + 1;
                }
            }
            var ordered = _counts.OrderByDescending(pair => pair.Value);
            foreach (var entry in ordered)
            {
                _output.WriteLine("{0} {1}", entry.Key, entry.Value);
            }
        }

        // https://medium.com/@joni2nja/evaluating-readline-using-system-io-pipelines-performance-in-c-part-2-b9d22c95254b

        [Benchmark]
        public async Task Optimized()
        {
            // Optimized version by Joel Mueller

            var reader = PipeReader.Create(_input.BaseStream, new StreamPipeReaderOptions(leaveOpen: true));

            while (true)
            {
                ReadResult result = await reader.ReadAsync();
                ReadOnlySequence<byte> buffer = result.Buffer;

                ProcessLine(ref buffer);

                reader.AdvanceTo(buffer.Start, buffer.End);

                if (result.IsCompleted) break;
            }

            await reader.CompleteAsync();

            var ordered = _counts.OrderByDescending(pair => pair.Value);
            foreach (var entry in ordered)
            {
                _output.WriteLine("{0} {1}", entry.Key.ToLowerInvariant(), entry.Value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string ProcessLine(ref ReadOnlySequence<byte> buffer)
        {
            string str = null;

            if (buffer.IsSingleSegment)
            {
                var span = buffer.FirstSpan;
                int consumed;
                while (span.Length > 0)
                {
                    var newLine = span.IndexOf(NewLine);

                    if (newLine == -1) break;

                    var line = span.Slice(0, newLine);
                    ParseLine(line);

                    consumed = line.Length + NewLine.Length;
                    span = span.Slice(consumed);
                    buffer = buffer.Slice(consumed);
                }
            }
            else
            {
                var sequenceReader = new SequenceReader<byte>(buffer);

                while (!sequenceReader.End)
                {
                    while (sequenceReader.TryReadTo(out ReadOnlySpan<byte> line, NewLine))
                    {
                        ParseLine(line);
                    }

                    buffer = buffer.Slice(sequenceReader.Position);
                    sequenceReader.Advance(buffer.Length);
                }
            }

            return str;

            void ParseLine(ReadOnlySpan<byte> line)
            {
                using var lineChars = SpanOwner<char>.Allocate(Encoding.UTF8.GetMaxCharCount(line.Length));
                var charsEncoded = Encoding.UTF8.GetChars(line, lineChars.Span);

                foreach (var token in lineChars.Span[..charsEncoded].Tokenize(' '))
                {
                    var chars = token.Trim();
                    if (chars.IsEmpty) continue;
                    var word = chars.ToString(); // TODO: not this
                    _counts[word] = _counts.GetValueOrDefault(word, 0) + 1;
                }
            }
        }

        private StreamReader _input;
        private readonly StreamWriter _output = StreamWriter.Null;
        private Dictionary<string, int> _counts;

        private static readonly byte[] NewLine = new byte[] { (byte)'\r', (byte)'\n' };

        [IterationSetup]
        public void IterSetup()
        {
            // simulate Console.In
            _input = File.OpenText("kjvbible.txt");
            _counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        [IterationCleanup]
        public void IterCleanup()
        {
            _input?.Dispose();
        }
    }
}
