using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace GTPGameServer
{
    public struct GTPProcessStartInfo
    {
        public string FileName;
        public string Args;
        public string WorkDir;

        public GTPProcessStartInfo(string fileName, string args, string workDir)
        {
            this.FileName = fileName;
            this.Args = args;
            this.WorkDir = workDir;
        }
    }

    public class GTPProcess
    {
        const string GTP_HEADER_PATTERN = "^[=?][0-9]+";

        Process process;
        ProcessStartInfo processStartInfo;

        int ID = 0;
        int currentCommandID = -1;
        GTPStatus currentCommandStatus;
        StringBuilder currentCommandOutput;
        Dictionary<int, GTPResult> uncompletedCommands;

        public bool HasExisted { get { return this.process.HasExited; } }
        public GTPProcessStartInfo ProcessStartInfo { get; }

        public GTPProcess(string path, params string[] args) : this(new GTPProcessStartInfo(path, MergeArgs(args), Environment.CurrentDirectory)) { }

        public GTPProcess(GTPProcessStartInfo startInfo)
        {
            this.processStartInfo = new ProcessStartInfo();
            this.processStartInfo.FileName = startInfo.FileName;
            this.processStartInfo.Arguments = startInfo.Args;
            this.processStartInfo.WorkingDirectory = startInfo.WorkDir;
            this.processStartInfo.UseShellExecute = false;
            this.processStartInfo.RedirectStandardInput = true;
            this.processStartInfo.RedirectStandardOutput = true;
            this.processStartInfo.CreateNoWindow = true;
            this.ProcessStartInfo = startInfo;

            this.currentCommandOutput = new StringBuilder();
            this.uncompletedCommands = new Dictionary<int, GTPResult>();
        }

        ~GTPProcess()
        {
            if (!this.process.HasExited)
                Kill();
        }

        public void Kill()
        {
            this.process.Kill();
        }

        public void Start()
        {
            this.process = new Process();
            this.process.StartInfo = this.processStartInfo;
            this.process.OutputDataReceived += Process_OutputDataReceived;
            this.process.Start();
            this.process.BeginOutputReadLine();
        }

        public GTPRequest SendCommand(string cmd, int timeout = -1)
        {
            return SendCommand(cmd, new string[0], timeout);
        }

        public GTPRequest SendCommand(string cmd, params string[] args)
        {
            return SendCommand(cmd, args, -1);
        }

        public GTPRequest SendCommand(string cmd, string[] args, int timeout = -1)
        {
            var result = new GTPResult(GTPStatus.Waiting, string.Empty);
            this.uncompletedCommands[this.ID] = result;
            var cmdLine = $"{this.ID} {cmd} {MergeArgs(args)}";
            this.process.StandardInput.WriteLine(cmdLine);
            var req = new GTPRequest(this.ID, cmdLine, result, Environment.TickCount, timeout);
            this.ID++;
            return req;
        }

        void Process_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data == null)
                return;
            if (this.currentCommandID == -1 && Regex.IsMatch(e.Data, GTP_HEADER_PATTERN))
            {
                this.currentCommandID = int.Parse(Regex.Match(e.Data, "[0-9]+").Value);
                this.currentCommandStatus = (e.Data[0] == '=') ? GTPStatus.Success : GTPStatus.Failed;
                var output = Regex.Replace(e.Data, GTP_HEADER_PATTERN, string.Empty);
                if (output.Length != 0 && output[0] == ' ')     // remove first half-width space
                    output = output[1..];
                this.currentCommandOutput.Append(output);
            }
            else if (e.Data == string.Empty)
            {
                var result = this.uncompletedCommands[this.currentCommandID];
                if (result.Status != GTPStatus.Timeout)
                {
                    result.Output = this.currentCommandOutput.ToString();
                    result.Status = this.currentCommandStatus;
                }
                this.uncompletedCommands.Remove(this.currentCommandID);
                this.currentCommandID = -1;
                this.currentCommandOutput.Clear();
            }
            else
            {
                if (this.currentCommandID != -1)
                    this.currentCommandOutput.Append(e.Data);
                else
                    throw new GTPInvalidDataFormatException("Invalid format data recieved. It does not start by \'=\' or \'?\'.", e.Data);
            }
        }

        static string MergeArgs(string[] args)
        {
            if (args.Length == 0)
                return string.Empty;
            var mergedArgs = new StringBuilder();
            for (var i = 0; i < args.Length - 1; i++)
                mergedArgs.Append($"{args[i]} ");
            mergedArgs.Append(args[args.Length - 1]);
            return mergedArgs.ToString();
        }
    }
}
