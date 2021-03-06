﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Tools.Properties;

namespace Microsoft.EntityFrameworkCore.Tools
{
    internal class Project
    {
        private readonly string _file;
        private readonly string _framework;
        private readonly string _configuration;

        public Project(string file, string framework, string configuration)
        {
            Debug.Assert(!string.IsNullOrEmpty(file), "file is null or empty.");

            _file = file;
            _framework = framework;
            _configuration = configuration;
            ProjectName = Path.GetFileName(file);
        }

        public string ProjectName { get; }

        public string AssemblyName { get; set; }
        public string OutputPath { get; set; }
        public string PlatformTarget { get; set; }
        public string ProjectAssetsFile { get; set; }
        public string ProjectDir { get; set; }
        public string RootNamespace { get; set; }
        public string TargetFileName { get; set; }
        public string TargetFrameworkMoniker { get; set; }

        public static Project FromFile(
            string file,
            string buildExtensionsDir,
            string framework = null,
            string configuration = null)
        {
            Debug.Assert(!string.IsNullOrEmpty(file), "file is null or empty.");

            if (buildExtensionsDir == null)
            {
                buildExtensionsDir = Path.Combine(Path.GetDirectoryName(file), "obj");
            }

            var efTargetsPath = Path.Combine(
                buildExtensionsDir,
                Path.GetFileName(file) + ".EntityFrameworkCore.targets");
            if (!File.Exists(efTargetsPath))
            {
                Reporter.WriteVerbose(string.Format(Resources.WritingFile, efTargetsPath));

                using (var input = typeof(Resources).GetTypeInfo().Assembly.GetManifestResourceStream(
                    "Microsoft.EntityFrameworkCore.Tools.Resources.EntityFrameworkCore.targets"))
                using (var output = File.OpenWrite(efTargetsPath))
                {
                    input.CopyTo(output);
                }
            }

            IDictionary<string, string> metadata;
            var metadataFile = Path.GetTempFileName();
            try
            {
                var propertyArg = "/property:EFProjectMetadataFile=" + metadataFile;
                if (configuration != null)
                {
                    propertyArg += ";TargetFramework=" + framework;
                }
                if (configuration != null)
                {
                    propertyArg += ";Configuration=" + configuration;
                }

                var args = new List<string>
                {
                    "msbuild",
                    "/target:GetEFProjectMetadata",
                    propertyArg,
                    "/verbosity:quiet",
                    "/nologo"
                };

                if (file != null)
                {
                    args.Add(file);
                }

                var exitCode = Exe.Run("dotnet", args);
                if (exitCode != 0)
                {
                    throw new CommandException(Resources.GetMetadataFailed);
                }

                metadata = File.ReadLines(metadataFile).Select(l => l.Split(new[] { ':' }, 2))
                    .ToDictionary(s => s[0], s => s[1].TrimStart());
            }
            finally
            {
                File.Delete(metadataFile);
            }

            var platformTarget = metadata["PlatformTarget"];
            if (platformTarget.Length == 0)
            {
                platformTarget = metadata["Platform"];
            }

            return new Project(file, framework, configuration)
            {
                AssemblyName = metadata["AssemblyName"],
                OutputPath = metadata["OutputPath"],
                PlatformTarget = platformTarget,
                ProjectAssetsFile = metadata["ProjectAssetsFile"],
                ProjectDir = metadata["ProjectDir"],
                RootNamespace = metadata["RootNamespace"],
                TargetFileName = metadata["TargetFileName"],
                TargetFrameworkMoniker = metadata["TargetFrameworkMoniker"]
            };
        }

        public void Build()
        {
            var args = new List<string>();

            args.Add("build");

            if (_file != null)
            {
                args.Add(_file);
            }

            // TODO: Only build for the first framework when unspecified
            if (_framework != null)
            {
                args.Add("--framework");
                args.Add(_framework);
            }

            if (_configuration != null)
            {
                args.Add("--configuration");
                args.Add(_configuration);
            }

            args.Add("/verbosity:quiet");
            args.Add("/nologo");


            var exitCode = Exe.Run("dotnet", args);
            if (exitCode != 0)
            {
                throw new CommandException(Resources.BuildFailed);
            }
        }
    }
}
