TalentTreeLayoutTests.Run();
EquipmentPresentationTests.Run();
ReleasePresentationTests.Run();
AssistantHomeTests.Run();
ControlScaleStateTests.Run();
ChatClientSetupTests.Run();
if (args.Length > 0)
{
    if (args.Length != 2 || args[0] != "--package")
        throw new ArgumentException("Use --package <absolute published Claudgar.exe path> to validate the release payload.");
    PackagePayloadTests.Run(args[1]);
}
