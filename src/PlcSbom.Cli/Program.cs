using System.Text;
using PlcSbom.Cli;

Console.OutputEncoding = new UTF8Encoding(false);
return await CliApp.RunAsync(args, CliEnvironment.FromProcess(), Console.Out, Console.Error);
