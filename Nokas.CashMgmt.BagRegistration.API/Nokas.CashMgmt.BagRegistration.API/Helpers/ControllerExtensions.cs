using Microsoft.AspNetCore.Mvc;
using Nokas.CashMgmt.BagRegistration.API.Middleware;
using Nokas.CashMgmt.BagRegistration.API.Models;

namespace Nokas.CashMgmt.BagRegistration.API.Helpers
{
    public static class ControllerExtensions
    {
        public static ActionResult<T> BuildValidationError<T>(this ControllerBase controller)
        {
            var correlationId = controller.HttpContext?.Items["CorrelationId"]?.ToString() ?? "N/A";

            var errorMessages = controller.ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();

            var error = new ErrorResponse
            {
                Title = "Validation Failed",
                Status = 400,
                CorrelationId = correlationId,
                Exception = string.Join(" | ", errorMessages)
            };

            return new BadRequestObjectResult(error);
        }
    }
}
