using System;
using System.Collections.Generic;
using System.Text;

namespace OrderProcessing.Shared.Exceptions;

public sealed class DataFileCorruptedException : Exception
{
    public DataFileCorruptedException(string filePath, Exception? innerException = null)
        : base($"Invalid JSON collection in file '{Path.GetFileName(filePath)}'.", innerException) { }
}
