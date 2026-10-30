if (args.Length != 1)
{
    Console.WriteLine("Uso: dotnet run -- \"tu_contraseña\"");
    return;
}

var hash = BCrypt.Net.BCrypt.HashPassword(args[0]);
Console.WriteLine(hash);
