﻿using System;
using System.Collections.Generic;
using log4net;
using Octopus.Cli.Util;
using Octopus.Client;

namespace Octopus.Cli.Importers
{
    public abstract class BaseImporter : IImporter
    {
        readonly FileSystemImporter fileSystemImporter;
        readonly ILog log;
        readonly IOctopusRepository repository;

        protected BaseImporter(IOctopusRepository repository, IOctopusFileSystem fileSystem, ILog log)
        {
            this.log = log;
            this.repository = repository;
            fileSystemImporter = new FileSystemImporter(fileSystem, log);
        }

        public ILog Log
        {
            get { return log; }
        }

        public IOctopusRepository Repository
        {
            get { return repository; }
        }

        public FileSystemImporter FileSystemImporter
        {
            get { return fileSystemImporter; }
        }

        public string FilePath { get; set; }

        public bool Validate(params string[] parameters)
        {
            var paramDictionary = ParseParameters(parameters);
            FilePath = paramDictionary["FilePath"];

            return Validate(paramDictionary);
        }

        public void Import(params string[] parameters)
        {
            var paramDictionary = ParseParameters(parameters);
            FilePath = paramDictionary["FilePath"];

            Import(paramDictionary);
        }

        protected virtual void Import(Dictionary<string, string> paramDictionary)
        {
        }

        protected virtual bool Validate(Dictionary<string, string> paramDictionary)
        {
            return true;
        }

        Dictionary<string, string> ParseParameters(IEnumerable<string> parameters)
        {
            var paramDictionary = new Dictionary<string, string>();
            foreach (var parameter in parameters)
            {
                var values = parameter.Split(new[] {'='});
                paramDictionary.Add(values[0], values[1]);
            }
            return paramDictionary;
        }

    }
}