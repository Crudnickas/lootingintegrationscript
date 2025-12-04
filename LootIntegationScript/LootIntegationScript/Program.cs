using System.IO.Compression;

Console.WriteLine("Welcome to Loot Integration");
string userInput = "";
bool validAnswer = false;
while (!validAnswer)
{
    userInput = AskQuestions();
    switch (userInput.ToLower())
    {
        case "1":
            AskFollowUps();
            Console.WriteLine("Process Finshed!");
            break;
        case "2":
            CloseApplication();
            break;
        case "3":
            Console.WriteLine(@"           __..--''``---....___   _..._    __
 /// //_.-'    .-/"";  `        ``<._  ``.''_ `. / // /
///_.-' _..--.'_    \                    `( ) ) // //
/ (_..-' // (< _     ;_..__               ; `' / ///
 / // // //  `-._,_)' // / ``--...____..-' /// / //
");
            break;
        default:
            Console.WriteLine($"Invalid selection: {userInput}, please enter 1 OR 2.");
            break;
    }
}

string AskQuestions()
{
    Console.WriteLine("How you like to proceed?");
    Console.WriteLine("1.) Generate Loot Table File.");
    Console.WriteLine("2.) Exit Program");
    return Console.ReadLine();
}

void AskFollowUps()
{
    Console.WriteLine("Please give me the rootfolder for your mod pack (Example Format: D:/Minecraft Modding Projects/TestModpack/ModPack/.minecraft)");
    string rootModPackPath = Console.ReadLine();
    if (rootModPackPath == string.Empty)
        rootModPackPath = "D:\\Minecraft Modding Projects\\TestModPack\\.minecraft";
    Console.WriteLine("Please give me where you would like the output .txt file to be generated (Example Format: D:/Minecraft Modding Projects/Output)");
    string outputPath = Console.ReadLine();
    if (outputPath == string.Empty)
        outputPath = "D:/Minecraft Modding Projects/Output";
    Console.WriteLine("How many items (Default is 8)");
    string itemsAmount = Console.ReadLine();
    if (itemsAmount == string.Empty)
        itemsAmount = "8";
    Console.WriteLine("What parts should we exempt (default is entities,blocks)");
    string exemptPathParts = Console.ReadLine();
    if (exemptPathParts == string.Empty)
        exemptPathParts = "entities,blocks";

    ReadJars(rootModPackPath, outputPath, itemsAmount, exemptPathParts);
}

void ReadJars(string rootModPackPath, string outputPath, string itemsAmount, string exemptPathParts)
{

    var outputFile = $@"{outputPath}/OutPut{DateTime.Now.ToFileTime()}.txt";
    List<LootTableFolder> allFolders = new List<LootTableFolder>();

    foreach (var jarPath in Directory.GetFiles($@"{rootModPackPath}/mods", "*.jar"))

    {

        Console.WriteLine($"\n=== Scanning {Path.GetFileName(jarPath)} ===");

        try

        {

            using FileStream fs = new FileStream(jarPath, FileMode.Open, FileAccess.Read);

            using ZipArchive archive = new ZipArchive(fs, ZipArchiveMode.Read);


            // 1. Find any entry containing loot_tables path

            var lootTableEntries = archive.Entries
                .Where(e =>
                    e.FullName.Contains("loot_tables/", StringComparison.OrdinalIgnoreCase) ||
                    e.FullName.Contains("loot_tables\\", StringComparison.OrdinalIgnoreCase))
                .ToList();


            if (lootTableEntries.Count == 0)
            {
                Console.WriteLine("No loot_tables found.");
                continue;
            }


            // 2. Group by root loot_tables folder
            // (Everything up to ".../loot_tables/")

            var grouped = lootTableEntries

                .GroupBy(entry =>
                {
                    string path = entry.FullName.Replace('\\', '/');
                    int idx = path.IndexOf("loot_tables/", StringComparison.OrdinalIgnoreCase);
                    return path.Substring(0, idx + "loot_tables/".Length);

                });


            foreach (var group in grouped)
            {

                string lootTableRoot = group.Key;  // e.g. "data/modid/loot_tables/"
                // Parent folder name = modid (data/<modid>/loot_tables/)
                string parentFolder = lootTableRoot

                    .Replace('\\', '/')
                    .Split('/', StringSplitOptions.RemoveEmptyEntries)
                    .Reverse()
                    .Skip(1) // skip "loot_tables" and the trailing slash
                    .FirstOrDefault() ?? "UNKNOWN";


                LootTableFolder folderData = new LootTableFolder
                {
                    ModJarName = Path.GetFileName(jarPath),
                    ParentFolderName = parentFolder,
                    LootTablesRoot = lootTableRoot
                };

                // 3. Collect JSON files relative to loot_tables path
                foreach (var entry in group)
                {
                    string path = entry.FullName.Replace('\\', '/');

                    if (!path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!path.Contains("loot_tables/", StringComparison.OrdinalIgnoreCase))
                        continue;

                    bool returnOutOfOutterLoop = false;
                    foreach (var exempt in exemptPathParts.Split(','))
                    {
                        if (path.Contains($"{exempt}/", StringComparison.OrdinalIgnoreCase))
                            returnOutOfOutterLoop = true;
                    }
                    if (returnOutOfOutterLoop)
                    {
                        continue;
                    }
                    string relative = path.Substring(lootTableRoot.Length);

                    relative = relative[..^5]; //Remove JSON
                    folderData.RelativeJsonPaths.Add(relative);
                }
                allFolders.Add(folderData);


                // Output

                Console.WriteLine($"\nLoot Table Parent: {folderData.ParentFolderName}");

                Console.WriteLine($"Root Path: {folderData.LootTablesRoot}");

                Console.WriteLine("JSON Files:");


                foreach (var json in folderData.RelativeJsonPaths)
                    Console.WriteLine($"  - {json}");
            }

        }

        catch (Exception ex)

        {

            Console.WriteLine($"Error scanning {jarPath}: {ex.Message}");

        }


    }
    using StreamWriter writer = new StreamWriter(outputFile, false);

    foreach (var folder in allFolders)

    {

        foreach (var relative in folder.RelativeJsonPaths)

        {

            string line = $"\"{folder.ParentFolderName}:{relative}\":  {itemsAmount},";

            writer.WriteLine(line);

        }

    }


    Console.WriteLine("Done! Output saved to: " + outputFile);
    Console.WriteLine("\n=== Loot Table Summary ===");


    var summary = allFolders

        .GroupBy(f => f.ParentFolderName)

        .Select(g => new

        {

            Parent = g.Key,

            Count = g.Sum(x => x.RelativeJsonPaths.Count)

        })

        .OrderByDescending(x => x.Count);


    foreach (var item in summary)

    {

        Console.WriteLine($"{item.Parent}: {item.Count}");

    }


    Console.WriteLine("=====================================");
}

void CloseApplication()
{
    Console.WriteLine("Exiting application with code 0.");
    Environment.Exit(0); // Exit with a success code
}


public class LootTableFolder

{

    public string ModJarName { get; set; }

    public string ParentFolderName { get; set; }

    public string LootTablesRoot { get; set; }

    public List<string> RelativeJsonPaths { get; set; } = new List<string>();


}