// Fast-Import / Fast-Export Rewriter to migrate bazaar bugtracking metdata
// properties to git.
// More info:
//     http://www.fusonic.net/en/blog/2013/03/26/migrating-from-bazaar-to-git/
//
// Licence: MIT X11 / BSD
//
// This version is customized for the Bugzilla project.
//
// Pipe the output from `bzr fast-export --no-plain --git-branch=<branch>`
// into this script, and the output of this script into `git fast-import`.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace migrate
{
    class FastImportRewriter
    {
        static readonly Regex dataLengthRegex = new Regex("data ([0-9]+)?", RegexOptions.Compiled);
        static readonly Regex bugLengthRegex = new Regex("property bugs ([0-9]+)", RegexOptions.Compiled);
        static int blockSize = 1024;
        static CommitPart commitPart;
        static Stream outputStream;

        public static void Main(string[] args)
        {
            using (outputStream = Console.OpenStandardOutput())
            {
                using (LineReader inputStream = new LineReader(Console.OpenStandardInput()))
                {
                    string s = null;
                    while ((s = inputStream.ReadLine()) != null)
                    {
                        if (ShouldSkipLine(s))
                            continue;
                        else if (s.StartsWith("reset "))
                        {
                            if (commitPart != null)
                                commitPart.Flush();

                            commitPart = new CommitPart();
                            commitPart.Reset = s;
                        }
                        else if (s.StartsWith("commit "))
                        {
                            if (commitPart != null)
                                commitPart.Flush();

                            commitPart = new CommitPart();
                            commitPart.Commit = s;
                        }
                        else if (s.StartsWith("mark "))
                            commitPart.Mark = s;
                        else if (s.StartsWith("committer "))
                            commitPart.Committer = s;
                        else if (s.StartsWith("author "))
                            commitPart.Author = s;
                        else if (s.StartsWith("data ") && (commitPart != null && commitPart.MessageOrig == null))
                        {
                            int dataLength = ParseDataLength(s);
                            commitPart.MessageOrig = ReadDataBlockAsString(inputStream, dataLength);
                        }
                        else if (s.StartsWith("from "))
                            commitPart.From = s;
                        else if (s.StartsWith("merge "))
                            commitPart.Merge = s;
                        else if (s.StartsWith("property bugs "))
                        {
                            int bugLength = ParseBugLength(s);
                            int bugHeadLength = ("property bugs " + bugLength.ToString() + " ").Length;

                            commitPart.Bug = s.Substring(bugHeadLength);
                            if (s.Length - bugHeadLength < bugLength)
                                commitPart.Bug += "\n" + ReadDataBlockAsString(inputStream, bugLength - (s.Length - bugHeadLength));
                        }
                        else if (s.StartsWith("R "))
                        {
                            // If directory, track the new name.
                            if (IsDirectory(s.Substring(2, s.IndexOf(" ", 2) - 2)))
                                dirs.Add(s.Substring(s.IndexOf(" ", 2) + 1));
                            else
                                commitPart.Renames.Add(s);
                        }
                        else if (s.StartsWith("D "))
                        {
                            if (!IsDirectory(s.Substring(2)))
                                commitPart.Deletes.Add(s);
                        }
                        else if (s.StartsWith("M "))
                        {
                            if (s.StartsWith("M 040000"))
                            {
                                // track directories
                                dirs.Add(s.Substring(11));

                                // Skip Directory
                                continue;
                            }

                            //Reached end of commit block, now rewrite the commit and output the data
                            if (commitPart != null)
                            {
                                commitPart.Flush();
                                commitPart = null;
                            }
                            WriteLine(s);
                        }
                        else if (s.StartsWith("data ") && commitPart == null)
                        {
                            int dataLength = ParseDataLength(s);
                            WriteLine(s);
                            int current = dataLength;
                            while (current - blockSize > 0)
                            {
                                var data = ReadDataBlock(inputStream, 1024);
                                Write(data);
                                current = current - blockSize;
                            }
                            if (current > 0)
                            {
                                var data = ReadDataBlock(inputStream, current);
                                Write(data);
                            }

                            WriteLine(string.Empty);
                        }
                        else
                            Console.Error.WriteLine("Skipping: " + s);
                    }
                }

                if (commitPart != null)
                    commitPart.Flush();
            }
        }

        private static bool IsDirectory(string s)
        {
            bool isDir = dirs.Contains(s);
            return isDir;
        }

        private static readonly HashSet<string> dirs = new HashSet<string>();

        static void Write(byte[] buffer)
        {
            outputStream.Write(buffer, 0, buffer.Length);
        }

        static readonly byte[] newline = Encoding.ASCII.GetBytes(Environment.NewLine);

        static void WriteLine(string s)
        {
            byte[] bytes = System.Text.UTF8Encoding.Default.GetBytes(s);
            outputStream.Write(bytes, 0, bytes.Length);
            outputStream.Write(newline, 0, newline.Length);
        }

        static string ReadDataBlockAsString(LineReader inputStream, int dataLength)
        {
            return System.Text.UTF8Encoding.Default.GetString(inputStream.ReadBytes(dataLength));
        }

        static byte[] ReadDataBlock(LineReader inputStream, int dataLength)
        {
            byte[] buffer = inputStream.ReadBytes(dataLength);
            return buffer;
        }

        static int ParseDataLength(string s)
        {
            var match = dataLengthRegex.Match(s);
            if (match.Success)
                return int.Parse(match.Groups[1].Value);
            return 0;
        }

        static int ParseBugLength(string s)
        {
            var match = bugLengthRegex.Match(s);
            if (match.Success)
                return int.Parse(match.Groups[1].Value);
            return 0;
        }

        static bool ShouldSkipLine(string s)
        {
            bool skip = s == string.Empty || s.StartsWith("feature") || s.StartsWith("property branch-nick");
            return skip;
        }

        class CommitPart
        {
            static readonly Regex origBugIdRegex = new Regex(@"[bB]ug\s+([0-9]+)", RegexOptions.Compiled);
            static readonly Regex bugsRegex = new Regex("https://bugzilla.mozilla.org/show_bug.cgi\\?id=([0-9]+)", RegexOptions.Compiled);

            public CommitPart()
            {
                Deletes = new List<string>();
                Renames = new List<string>();
            }

            public string Reset { get; set; }

            public string Commit { get; set; }

            public string Mark { get; set; }

            public string Committer { get; set; }

            public string Author { get; set; }

            public string MessageOrig { get; set; }

            public string MessageNew { get; private set; }

            public string From { get; set; }

            public string Merge { get; set; }

            public string Bug { get; set; }

            public List<string> Deletes
            {
                get;
                private set;
            }

            public List<string> Renames
            {
                get;
                private set;
            }

            public void RewriteMessageWithBug()
            {
                if (MessageOrig == null)
                    return;

                if (Bug == null)
                {
                    MessageNew = "data " + System.Text.UTF8Encoding.Default.GetByteCount(MessageOrig).ToString() + "\n" + MessageOrig;
                    return;
                }

                string messageAddendum = "";
                HashSet<string> bugsInMessage = new HashSet<string>();

                foreach (Match match in origBugIdRegex.Matches(MessageOrig))
                    bugsInMessage.Add(match.Groups[1].Value);

                foreach (Match match in bugsRegex.Matches(Bug))
                {
                    if (!bugsInMessage.Contains(match.Groups[1].Value))
                    {
                        if (messageAddendum == "" && MessageOrig[MessageOrig.Length-1] != '\n')
                            messageAddendum = "\n";
                        messageAddendum += "\n" + match.Value;
                    }
                }

                string newMessageString = MessageOrig + messageAddendum;

                int byteLength = System.Text.UTF8Encoding.Default.GetByteCount(newMessageString);
                MessageNew = "data " + byteLength.ToString() + "\n" + newMessageString;
            }

            public void Flush()
            {
                commitPart.RewriteMessageWithBug();

                if (Reset != null)
                {
                    var l = "reset refs/tags/".Length;
                    string reset = Reset.Substring(0, l) + Reset.Substring(l, Reset.Length - l).Replace(' ', '_');

                    WriteLine(reset);
                    WriteLine(From);
                    WriteLine(string.Empty);
                    return;
                }

                WriteLine(Commit);
                WriteLine(Mark);
                if (Author != null)
                    WriteLine(Author);
                WriteLine(Committer);
                WriteLine(MessageNew);

                if (From != null)
                    WriteLine(From);


                if (Merge != null)
                    WriteLine(Merge);

                foreach (string renamed in Renames)
                    WriteLine(renamed);

                foreach (string deleted in Deletes)
                    WriteLine(deleted);
            }
        }
    }

    public class LineReader : BinaryReader
    {
        public LineReader(Stream stream)
            : base(stream, Encoding.UTF8)
        {
        }

        static readonly byte[] newline = Encoding.ASCII.GetBytes(Environment.NewLine);

        public string ReadLine()
        {
            List<byte> buffer = new List<byte>(64);
            try
            {
                while (true)
                {
                    byte lastByte = base.ReadByte();
                    if (lastByte == newline[0])
                        return System.Text.UTF8Encoding.Default.GetString(buffer.ToArray());
                    buffer.Add(lastByte);
                }
            }
            catch (EndOfStreamException)
            {
                return null;
            }
        }
    }
}
