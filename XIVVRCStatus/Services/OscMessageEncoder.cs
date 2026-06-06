using System.IO;
using System.Text;

namespace XIVVRCStatus.Services;

public static class OscMessageEncoder
{
    public static byte[] BuildChatboxInput(string text, bool sendImmediately, bool playNotificationSound)
    {
        using var stream = new MemoryStream();
        WritePaddedString(stream, "/chatbox/input");
        WritePaddedString(stream, $",s{(sendImmediately ? 'T' : 'F')}{(playNotificationSound ? 'T' : 'F')}");
        WritePaddedString(stream, text);
        return stream.ToArray();
    }

    private static void WritePaddedString(Stream stream, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        stream.Write(bytes);
        stream.WriteByte(0);

        while (stream.Length % 4 != 0)
        {
            stream.WriteByte(0);
        }
    }
}
