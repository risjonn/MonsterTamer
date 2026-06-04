namespace MonsterTamer
{
    public readonly struct LogEntry
    {
        public readonly string       Text;
        public readonly LogEntryType Type;

        public LogEntry(string text, LogEntryType type)
        {
            Text = text;
            Type = type;
        }
    }
}
