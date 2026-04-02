using Grim.Tools.ContentValidation;
using Grim.Tools.Models;

var command = ParseCommand(args, out var options);
if (command is null || options is null)
{
	PrintUsage();
	return 1;
}

switch (command)
{
	case "content validate":
	{
		var resolvedRepoRoot = options.RepoRoot ?? ResolveRepositoryRoot();
		if (resolvedRepoRoot is null)
		{
			Console.Error.WriteLine("Could not resolve repository root. Provide --repo-root <path>.");
			return 1;
		}

		var validator = new ContentValidator();
		var result = validator.Validate(resolvedRepoRoot);
		return result.Success ? 0 : 1;
	}
	case "models import":
	{
		var resolvedRepoRoot = options.RepoRoot ?? ResolveRepositoryRoot();
		if (resolvedRepoRoot is null)
		{
			Console.Error.WriteLine("Could not resolve repository root. Provide --repo-root <path>.");
			return 1;
		}

		var converter = new ModelConvertService();
		var importer = new ModelImportService(converter);
		var result = importer.Import(resolvedRepoRoot, options.ModelIdFilter);
		if (!result.Success)
		{
			Console.Error.WriteLine(result.Message);
			return 1;
		}

		Console.WriteLine(result.Message);
		return 0;
	}
	case "models convert":
	{
		if (string.IsNullOrWhiteSpace(options.InputFbx) || string.IsNullOrWhiteSpace(options.OutputGltf))
		{
			Console.Error.WriteLine("models convert requires <input.fbx> <output.gltf>");
			return 1;
		}

		var converter = new ModelConvertService();
		var result = converter.Convert(options.InputFbx, options.OutputGltf);
		if (!result.Success)
		{
			Console.Error.WriteLine(result.Message);
			return 1;
		}

		Console.WriteLine(result.Message);
		return 0;
	}
	case "models scaffold":
	{
		var resolvedRepoRoot = options.RepoRoot ?? ResolveRepositoryRoot();
		if (resolvedRepoRoot is null)
		{
			Console.Error.WriteLine("Could not resolve repository root. Provide --repo-root <path>.");
			return 1;
		}

		var generator = new PlaceholderMeshGenerator();
		var result = generator.Generate(resolvedRepoRoot, options.ShapesFilter);
		if (!result.Success)
		{
			Console.Error.WriteLine(result.Message);
			return 1;
		}

		Console.WriteLine(result.Message);
		return 0;
	}
	case "help":
		PrintUsage();
		return 0;
	default:
		Console.Error.WriteLine($"Unknown command: {command}");
		PrintUsage();
		return 1;
}

static string? ParseCommand(string[] args, out CliOptions? options)
{
	options = new CliOptions();
	var commandParts = new List<string>();
	var extraParts = new List<string>();

	for (var i = 0; i < args.Length; i++)
	{
		if (string.Equals(args[i], "--repo-root", StringComparison.OrdinalIgnoreCase))
		{
			if (i + 1 >= args.Length)
			{
				options = null;
				return null;
			}

			options.RepoRoot = args[i + 1];
			i++;
			continue;
		}

		if (string.Equals(args[i], "--id", StringComparison.OrdinalIgnoreCase))
		{
			if (i + 1 >= args.Length)
			{
				options = null;
				return null;
			}

			options.ModelIdFilter = args[i + 1];
			i++;
			continue;
		}

		if (string.Equals(args[i], "--shapes", StringComparison.OrdinalIgnoreCase))
		{
			if (i + 1 >= args.Length)
			{
				options = null;
				return null;
			}

			options.ShapesFilter = args[i + 1];
			i++;
			continue;
		}

		commandParts.Add(args[i]);
	}

	if (commandParts.Count == 0)
	{
		return "help";
	}

	if (commandParts.Count >= 2 && string.Equals(commandParts[0], "models", StringComparison.OrdinalIgnoreCase) && string.Equals(commandParts[1], "convert", StringComparison.OrdinalIgnoreCase))
	{
		if (commandParts.Count > 2)
		{
			extraParts.AddRange(commandParts.Skip(2));
		}

		if (extraParts.Count >= 2)
		{
			options.InputFbx = extraParts[0];
			options.OutputGltf = extraParts[1];
		}

		return "models convert";
	}

	if (commandParts.Count >= 2 && string.Equals(commandParts[0], "models", StringComparison.OrdinalIgnoreCase) && string.Equals(commandParts[1], "import", StringComparison.OrdinalIgnoreCase))
	{
		return "models import";
	}

	if (commandParts.Count >= 2 && string.Equals(commandParts[0], "models", StringComparison.OrdinalIgnoreCase) && string.Equals(commandParts[1], "scaffold", StringComparison.OrdinalIgnoreCase))
	{
		return "models scaffold";
	}

	if (commandParts.Count >= 2 && string.Equals(commandParts[0], "content", StringComparison.OrdinalIgnoreCase) && string.Equals(commandParts[1], "validate", StringComparison.OrdinalIgnoreCase))
	{
		return "content validate";
	}

	return string.Join(' ', commandParts).Trim().ToLowerInvariant();
}

static string? ResolveRepositoryRoot()
{
	var current = new DirectoryInfo(Environment.CurrentDirectory);
	while (current is not null)
	{
		var readme = Path.Combine(current.FullName, "README.md");
		var content = Path.Combine(current.FullName, "content");
		if (File.Exists(readme) && Directory.Exists(content))
		{
			return current.FullName;
		}

		current = current.Parent;
	}

	return null;
}

static void PrintUsage()
{
	Console.WriteLine("Grim.Tools - Cross-platform content tooling");
	Console.WriteLine();
	Console.WriteLine("Usage:");
	Console.WriteLine("  dotnet run --project src/Grim.Tools/Grim.Tools.csproj -- content validate [--repo-root <path>]");
	Console.WriteLine("  dotnet run --project src/Grim.Tools/Grim.Tools.csproj -- models import [--id <model_id>] [--repo-root <path>]");
	Console.WriteLine("  dotnet run --project src/Grim.Tools/Grim.Tools.csproj -- models scaffold [--shapes all|ground_tile_v1,rock_v1] [--repo-root <path>]");
	Console.WriteLine("  dotnet run --project src/Grim.Tools/Grim.Tools.csproj -- models convert <input.fbx> <output.gltf>");
}

internal sealed class CliOptions
{
	public string? RepoRoot { get; set; }
	public string? ModelIdFilter { get; set; }
	public string? ShapesFilter { get; set; }
	public string? InputFbx { get; set; }
	public string? OutputGltf { get; set; }
}
