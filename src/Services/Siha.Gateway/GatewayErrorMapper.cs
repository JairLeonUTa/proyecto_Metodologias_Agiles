using Ocelot.Errors;
using Ocelot.Responder;

namespace Siha.Gateway;

internal sealed class GatewayErrorMapper : IErrorsToHttpStatusCodeMapper
{
    private static readonly IErrorsToHttpStatusCodeMapper Defecto = new ErrorsToHttpStatusCodeMapper();

    public int Map(List<Error> errors)
    {
        if (errors.Any(e => e.Code == OcelotErrorCode.ConnectionToDownstreamServiceError))
        {
            return 503;
        }
        return Defecto.Map(errors);
    }
}