using PackageBuild;

new BuildWindows().GenerateBuild();
new BuildLinux().GenerateBuild();

Console.WriteLine("Done.");
return 0;
