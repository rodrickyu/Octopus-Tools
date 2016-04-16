using System;

namespace Octopus.Cli.Commands
{
    public interface IPackageVersionResolver
    {
        void AddFolder(string folderPath);
        void Add(string stepNameAndVersion);
        void Add(string stepName, string packageVersion);
        void Default(string packageVersion);
        string ResolveVersion(string stepName);
    }
}