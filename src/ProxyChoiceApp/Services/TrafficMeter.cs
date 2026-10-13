using System;

namespace ProxyChoiceApp.Services;

public sealed class TrafficMeter
{
    private TrafficSnapshot? _previous;
    private TimeSpan _previousTime;
    public long ReceivedBytes { get; private set; }
    public long SentBytes { get; private set; }
    public double ReceiveBytesPerSecond { get; private set; }
    public double SendBytesPerSecond { get; private set; }

    public void Reset()
    {
        _previous = null; _previousTime = default;
        ReceivedBytes = SentBytes = 0;
        Stop();
    }

    public void Stop() => ReceiveBytesPerSecond = SendBytesPerSecond = 0;

    public void Sample(TrafficSnapshot snapshot, TimeSpan elapsed)
    {
        ReceivedBytes = Math.Max(0, snapshot.Downloaded);
        SentBytes = Math.Max(0, snapshot.Uploaded);
        if (_previous is null) { _previous = snapshot; _previousTime = elapsed; Stop(); return; }
        var seconds = (elapsed - _previousTime).TotalSeconds;
        if (seconds <= 0) return;
        // Counters reset when a new core starts. Never turn that into a negative speed.
        ReceiveBytesPerSecond = Math.Max(0, ReceivedBytes - _previous.Downloaded) / seconds;
        SendBytesPerSecond = Math.Max(0, SentBytes - _previous.Uploaded) / seconds;
        _previous = snapshot; _previousTime = elapsed;
    }
}
