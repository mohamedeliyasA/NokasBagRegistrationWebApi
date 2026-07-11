using System.Text;

namespace Nokas.CashMgmt.BagRegistration.API.Helpers
{
    public static class ArchiveHelper
    {
        public static async Task<string> ReadRawBodyAsStringAsync(this HttpRequest request)
        {
            // Assume buffering is already enabled in the controller
            if (!request.Body.CanSeek)
            {
                throw new InvalidOperationException("Request body is not seekable. Call EnableBuffering() first.");
            }

            request.Body.Position = 0;

            using var reader = new StreamReader(request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
            var body = await reader.ReadToEndAsync();

            request.Body.Position = 0; // Reset for model binding or other middlewares
            return body;
        }
    }

}
