using System.ComponentModel.DataAnnotations;
using StreamManager.App.ViewModels;
using Xunit;

namespace StreamManager.Core.Tests.Presets;

public class SavePresetDialogViewModelTests
{
    [Fact]
    public void EmptyName_IsInvalid_AndConfirmDisabled()
    {
        var vm = new SavePresetDialogViewModel();

        Assert.False(vm.CanConfirm);
        Assert.False(vm.ConfirmCommand.CanExecute(null));
        Assert.True(vm.HasErrors);
    }

    [Fact]
    public void Name_At80Chars_IsValid()
    {
        var vm = new SavePresetDialogViewModel();
        vm.Name = new string('x', 80);
        Assert.False(vm.HasErrors);
        Assert.True(vm.CanConfirm);
    }

    [Fact]
    public void Name_Over80Chars_IsInvalid()
    {
        var vm = new SavePresetDialogViewModel();
        vm.Name = new string('x', 81);

        Assert.True(vm.HasErrors);
        Assert.False(vm.CanConfirm);
    }

    [Fact]
    public void DuplicateName_IsReportedButNotAValidationError()
    {
        var vm = new SavePresetDialogViewModel(new[] { "Elden Ring" });
        vm.Name = "elden ring"; // case-insensitive match

        Assert.False(vm.HasErrors);
        Assert.True(vm.CanConfirm);
        Assert.True(vm.NameMatchesExisting);
    }

    [Fact]
    public void NonDuplicateName_NameMatchesExistingIsFalse()
    {
        var vm = new SavePresetDialogViewModel(new[] { "Other" });
        vm.Name = "New name";

        Assert.False(vm.NameMatchesExisting);
    }

    [Fact]
    public void Confirm_SetsResult_AndFiresClosed()
    {
        var vm = new SavePresetDialogViewModel();
        vm.Name = "  trimmed  ";

        var closed = false;
        vm.Closed += (_, _) => closed = true;
        vm.ConfirmCommand.Execute(null);

        Assert.True(closed);
        Assert.Equal("trimmed", vm.Result);
        Assert.False(vm.WasCancelled);
    }

    [Fact]
    public void Cancel_LeavesResultNull_AndFiresClosed()
    {
        var vm = new SavePresetDialogViewModel();
        vm.Name = "something";

        var closed = false;
        vm.Closed += (_, _) => closed = true;
        vm.CancelCommand.Execute(null);

        Assert.True(closed);
        Assert.Null(vm.Result);
        Assert.True(vm.WasCancelled);
    }
}
