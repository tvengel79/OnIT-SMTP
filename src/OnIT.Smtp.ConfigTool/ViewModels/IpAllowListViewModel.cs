using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OnIT.Smtp.ConfigTool.Services;
using OnIT.Smtp.Core.Configuration;

namespace OnIT.Smtp.ConfigTool.ViewModels;

public partial class IpAllowListViewModel : ObservableObject
{
    [ObservableProperty] private IpAllowRuleKind _newRuleKind = IpAllowRuleKind.Single;
    [ObservableProperty] private string _newAddress = string.Empty;
    [ObservableProperty] private string _newRangeEnd = string.Empty;
    [ObservableProperty] private int _newPrefixLength = 24;
    [ObservableProperty] private string _newDescription = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private IpAllowRule? _selectedRule;

    public IReadOnlyList<IpAllowRuleKind> RuleKinds { get; } = Enum.GetValues<IpAllowRuleKind>();
    public ObservableCollection<IpAllowRule> Rules { get; } = new();

    public IpAllowListViewModel()
    {
        foreach (var rule in ConfigurationContext.Instance.Current.IpAllowRules) Rules.Add(rule);
    }

    [RelayCommand]
    private void AddRule()
    {
        var rule = NewRuleKind switch
        {
            IpAllowRuleKind.Single => IpAllowRule.ForSingle(NewAddress, NewDescription),
            IpAllowRuleKind.Range => IpAllowRule.ForRange(NewAddress, NewRangeEnd, NewDescription),
            IpAllowRuleKind.Cidr => IpAllowRule.ForCidr(NewAddress, NewPrefixLength, NewDescription),
            _ => throw new InvalidOperationException()
        };

        try
        {
            rule.Validate();
        }
        catch (FormatException ex)
        {
            StatusMessage = ex.Message;
            return;
        }

        Rules.Add(rule);
        ConfigurationContext.Instance.Current.IpAllowRules.Add(rule);
        ConfigurationContext.Instance.Save();

        NewAddress = string.Empty;
        NewRangeEnd = string.Empty;
        NewDescription = string.Empty;
        StatusMessage = "Rule added.";
    }

    [RelayCommand]
    private void RemoveSelected()
    {
        if (SelectedRule is null) return;

        ConfigurationContext.Instance.Current.IpAllowRules.Remove(SelectedRule);
        Rules.Remove(SelectedRule);
        ConfigurationContext.Instance.Save();
    }

    [RelayCommand]
    private void ToggleSelectedEnabled()
    {
        if (SelectedRule is null) return;

        SelectedRule.Enabled = !SelectedRule.Enabled;
        ConfigurationContext.Instance.Save();

        // Refresh the bound list item.
        var index = Rules.IndexOf(SelectedRule);
        if (index >= 0)
        {
            Rules.RemoveAt(index);
            Rules.Insert(index, SelectedRule);
        }
    }
}
