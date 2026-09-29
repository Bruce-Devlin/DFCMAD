using System.Text;
using DFCMAD.Core.Services;

namespace DFCMAD.Core.Logging;

public sealed class FileLogWriter
{
    private readonly IAppPaths _paths;
    private readonly object _gate = new();
    private readonly long _maxBytes;
    private string? _currentFile;

    public FileLogWriter(IAppPaths paths, long maxBytes = 2 * 1024 * 1024)
    {
        _paths = paths;
        _maxBytes = maxBytes;
    }

    public string LogDirectory => _paths.LogDirectory;

    public void Write(string line)
    {
        lock (_gate)
        {
            Directory.CreateDirectory(_paths.LogDirectory);
            var file = GetCurrentFile();
            File.AppendAllText(file, line + Environment.NewLine, Encoding.UTF8);
        }
    }

    private string GetCurrentFile()
    {
        var path = Path.Combine(_paths.LogDirectory, $"dfcmad-{DateTimeOffset.Now:yyyyMMdd}.log");

        if (_currentFile is not null && File.Exists(_currentFile) && new FileInfo(_currentFile).Length < _maxBytes)
        {
            return _currentFile;
        }

        if (!File.Exists(path) || new FileInfo(path).Length < _maxBytes)
        {
            _currentFile = path;
            return path;
        }

        var index = 1;
        while (true)
        {
            var candidate = Path.Combine(_paths.LogDirectory, $"dfcmad-{DateTimeOffset.Now:yyyyMMdd}-{index}.log");
            if (!File.Exists(candidate) || new FileInfo(candidate).Length < _maxBytes)
            {
                _currentFile = candidate;
                return candidate;
            }

            index++;
        }
    }
}
