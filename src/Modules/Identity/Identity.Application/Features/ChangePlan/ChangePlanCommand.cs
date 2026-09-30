using BuildingBlocks.Results;
using Identity.Domain;
using MediatR;

namespace Identity.Application.Features.ChangePlan;

/// <summary>
/// Switches the tenant's subscription plan. In production this is where a
/// billing provider (Stripe, Paddle, …) webhook would land; here it is a
/// direct owner action so the plan limits can be exercised end to end.
/// </summary>
public sealed record ChangePlanCommand(SubscriptionPlan Plan) : IRequest<Result<ChangePlanResponse>>;

public sealed record ChangePlanResponse(Guid TenantId, string Plan, int? MaxUsers, int? MaxProducts);
