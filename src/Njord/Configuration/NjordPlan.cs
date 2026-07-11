namespace Njord.Configuration;

/// <summary>Kachelmann API subscription plans. <see cref="Custom"/> requires an explicit budget override.</summary>
public enum NjordPlan
{
    Hobby,
    BusinessStarter,
    BusinessStandard,
    BusinessProfessional,
    BusinessEnterprise,
    Custom,
}
