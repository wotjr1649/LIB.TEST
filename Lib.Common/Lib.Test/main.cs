class Program
{
    static async Task Main(string[] args)
    {
        var testRunner = new LogTestRunner();
        await testRunner.RunAsync(args);
    }
}