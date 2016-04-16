﻿using System;

namespace Octopus.Cli.Util
{
    public static class NumericExtensions
    {
        const long Kilobyte = 1024;
        const long Megabyte = 1024*Kilobyte;
        const long Gigabyte = 1024*Megabyte;
        const long Terabyte = 1024*Gigabyte;

        public static string ToFileSizeString(this long bytes)
        {
            return ToFileSizeString(bytes <= 0 ? 0 : (ulong)bytes);
        }

        public static string ToFileSizeString(this ulong bytes)
        {
            if (bytes > Terabyte) return (bytes/Terabyte).ToString("0 TB");
            if (bytes > Gigabyte) return (bytes/Gigabyte).ToString("0 GB");
            if (bytes > Megabyte) return (bytes/Megabyte).ToString("0 MB");
            if (bytes > Kilobyte) return (bytes/Kilobyte).ToString("0 KB");
            return bytes + " bytes";
        }
    }
}