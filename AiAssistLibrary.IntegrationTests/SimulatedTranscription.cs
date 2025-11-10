using System.Linq;

namespace AiAssistLibrary.IntegrationTests;

public sealed record SimulatedUtterance(string SpeakerId, string Text, TimeSpan Start, TimeSpan End);

public static class SimulatedTranscription
{
	public static IReadOnlyList<SimulatedUtterance> StandupSample => new[]
	{
		new SimulatedUtterance("S1", "Good morning team.", TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(3)),
		new SimulatedUtterance("S2", "Can we finalize the sprint plan?", TimeSpan.FromSeconds(3),
			TimeSpan.FromSeconds(8)),
		new SimulatedUtterance("S1", "We deployed the new payments service yesterday.", TimeSpan.FromSeconds(8),
			TimeSpan.FromSeconds(14)),
		new SimulatedUtterance("S3", "What are the current error rates?", TimeSpan.FromSeconds(14),
			TimeSpan.FromSeconds(19)),
		new SimulatedUtterance("S2", "It's stable, right?", TimeSpan.FromSeconds(19), TimeSpan.FromSeconds(23)),
		new SimulatedUtterance("S1", "I think logs look clean.", TimeSpan.FromSeconds(23), TimeSpan.FromSeconds(27)),
		new SimulatedUtterance("S3", "How do we add tracing?", TimeSpan.FromSeconds(27), TimeSpan.FromSeconds(31)),
	};

	public static IReadOnlyList<SimulatedUtterance> SupportTicketSample => new[]
	{
		new SimulatedUtterance("AGENT", "Hello, thanks for contacting support.", TimeSpan.FromSeconds(0),
			TimeSpan.FromSeconds(2)),
		new SimulatedUtterance("USER", "Why is my account locked?", TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(6)),
		new SimulatedUtterance("AGENT", "Let me check.", TimeSpan.FromSeconds(6), TimeSpan.FromSeconds(8)),
		new SimulatedUtterance("USER", "Can you unlock it now?", TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(12)),
		new SimulatedUtterance("AGENT", "We can request a reset.", TimeSpan.FromSeconds(12), TimeSpan.FromSeconds(15)),
		new SimulatedUtterance("USER", "It's urgent, okay?", TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(18)),
	};

	/// <summary>
	/// The rule detector will fall back on low confidence for this question. It will then be sent to Azure for review.
	/// </summary>
	public static IReadOnlyList<SimulatedUtterance> LowConfidenceOfQuestion => new[]
	{
		new SimulatedUtterance("S1", "Hey, what's the weather?", TimeSpan.FromSeconds(0),
			TimeSpan.FromSeconds(2)),
	};


	// Explain the difference between class and struct in C sharp.
	public static IReadOnlyList<SimulatedUtterance> ImperativeRequests => new[]
	{
		new SimulatedUtterance("S1", "Explain the difference between class and struct in C sharp.", TimeSpan.FromSeconds(0),
			TimeSpan.FromSeconds(2)),
		new SimulatedUtterance("S1", "Explain dependency injection in .NET applications.", TimeSpan.FromSeconds(0),
			TimeSpan.FromSeconds(2)),
		new SimulatedUtterance("S1", "Explain the difference between interface and abstract class.", TimeSpan.FromSeconds(0),
			TimeSpan.FromSeconds(2)),


		// Explain the difference between interface and abstract class.
	};

	// Extended list of imperative coding requests as a separate transcription sample.
	public static IReadOnlyList<SimulatedUtterance> ImperativeCodingRequests => new[]
	{
		new SimulatedUtterance("S1", "Define a variable of type int and assign it a value.", TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(2)),
		new SimulatedUtterance("S1", "Declare a constant called Pi with the value 3.14.", TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4)),
		new SimulatedUtterance("S1", "Create a method that prints \"Hello World\" to the console.", TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(6)),
		new SimulatedUtterance("S1", "Write a method that adds two integers and returns the result.", TimeSpan.FromSeconds(6), TimeSpan.FromSeconds(8)),
		new SimulatedUtterance("S1", "Write a function that takes a string and returns its length.", TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(10)),
		new SimulatedUtterance("S1", "Create a for loop that counts from one to ten.", TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(12)),
		new SimulatedUtterance("S1", "Use a while loop to print numbers from one to five.", TimeSpan.FromSeconds(12), TimeSpan.FromSeconds(14)),
		new SimulatedUtterance("S1", "Create an if statement that checks whether a number is even.", TimeSpan.FromSeconds(14), TimeSpan.FromSeconds(16)),
		new SimulatedUtterance("S1", "Implement a switch statement for the days of the week.", TimeSpan.FromSeconds(16), TimeSpan.FromSeconds(18)),
		new SimulatedUtterance("S1", "Write a try–catch block that handles a divide-by-zero exception.", TimeSpan.FromSeconds(18), TimeSpan.FromSeconds(20)),
		new SimulatedUtterance("S1", "Define a class named Person with properties Name and Age.", TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(22)),
		new SimulatedUtterance("S1", "Instantiate an object of the Person class and print its name.", TimeSpan.FromSeconds(22), TimeSpan.FromSeconds(24)),
		new SimulatedUtterance("S1", "Create a constructor that initializes both Name and Age.", TimeSpan.FromSeconds(24), TimeSpan.FromSeconds(26)),
		new SimulatedUtterance("S1", "Add a method to the Person class that returns a greeting message.", TimeSpan.FromSeconds(26), TimeSpan.FromSeconds(28)),
		new SimulatedUtterance("S1", "Inherit from the Person class to create an Employee subclass.", TimeSpan.FromSeconds(28), TimeSpan.FromSeconds(30)),
		new SimulatedUtterance("S1", "Override the greeting method in the Employee class.", TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(32)),
		new SimulatedUtterance("S1", "Declare an interface called IVehicle with a StartEngine method.", TimeSpan.FromSeconds(32), TimeSpan.FromSeconds(34)),
		new SimulatedUtterance("S1", "Implement the IVehicle interface in a Car class.", TimeSpan.FromSeconds(34), TimeSpan.FromSeconds(36)),
		new SimulatedUtterance("S1", "Create a static method that calculates factorial recursively.", TimeSpan.FromSeconds(36), TimeSpan.FromSeconds(38)),
		new SimulatedUtterance("S1", "Write an extension method for the string type that reverses text.", TimeSpan.FromSeconds(38), TimeSpan.FromSeconds(40)),
		new SimulatedUtterance("S1", "Use LINQ to select all even numbers from an integer list.", TimeSpan.FromSeconds(40), TimeSpan.FromSeconds(42)),
		new SimulatedUtterance("S1", "Create a generic method that swaps two variables.", TimeSpan.FromSeconds(42), TimeSpan.FromSeconds(44)),
		new SimulatedUtterance("S1", "Implement a List<string> and add three names to it.", TimeSpan.FromSeconds(44), TimeSpan.FromSeconds(46)),
		new SimulatedUtterance("S1", "Sort a list of integers in descending order.", TimeSpan.FromSeconds(46), TimeSpan.FromSeconds(48)),
		new SimulatedUtterance("S1", "Remove duplicate elements from a list using LINQ.", TimeSpan.FromSeconds(48), TimeSpan.FromSeconds(50)),
		new SimulatedUtterance("S1", "Write a lambda expression that multiplies two numbers.", TimeSpan.FromSeconds(50), TimeSpan.FromSeconds(52)),
		new SimulatedUtterance("S1", "Use the Action delegate to point to a void method.", TimeSpan.FromSeconds(52), TimeSpan.FromSeconds(54)),
		new SimulatedUtterance("S1", "Use the Func<int,int,int> delegate to perform addition.", TimeSpan.FromSeconds(54), TimeSpan.FromSeconds(56)),
		new SimulatedUtterance("S1", "Subscribe to and raise a custom event in C#.", TimeSpan.FromSeconds(56), TimeSpan.FromSeconds(58)),
		new SimulatedUtterance("S1", "Create an asynchronous method that fetches data using HttpClient.", TimeSpan.FromSeconds(58), TimeSpan.FromSeconds(60)),
		new SimulatedUtterance("S1", "Await an asynchronous call inside a try–catch block.", TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(62)),
		new SimulatedUtterance("S1", "Demonstrate how to cancel an asynchronous operation.", TimeSpan.FromSeconds(62), TimeSpan.FromSeconds(64)),
		new SimulatedUtterance("S1", "Write code that reads text from a file using StreamReader.", TimeSpan.FromSeconds(64), TimeSpan.FromSeconds(66)),
		new SimulatedUtterance("S1", "Write code that writes text to a file using StreamWriter.", TimeSpan.FromSeconds(66), TimeSpan.FromSeconds(68)),
		new SimulatedUtterance("S1", "Serialize an object to JSON using System.Text.Json.", TimeSpan.FromSeconds(68), TimeSpan.FromSeconds(70)),
		new SimulatedUtterance("S1", "Deserialize JSON back into an object.", TimeSpan.FromSeconds(70), TimeSpan.FromSeconds(72)),
		new SimulatedUtterance("S1", "Demonstrate dependency injection with a simple service class.", TimeSpan.FromSeconds(72), TimeSpan.FromSeconds(74)),
		new SimulatedUtterance("S1", "Register a singleton service in an ASP.NET Core application.", TimeSpan.FromSeconds(74), TimeSpan.FromSeconds(76)),
		new SimulatedUtterance("S1", "Add custom middleware to log incoming HTTP requests.", TimeSpan.FromSeconds(76), TimeSpan.FromSeconds(78)),
		new SimulatedUtterance("S1", "Configure routing for a minimal API endpoint /hello.", TimeSpan.FromSeconds(78), TimeSpan.FromSeconds(80)),
		new SimulatedUtterance("S1", "Create and apply a custom attribute to a class.", TimeSpan.FromSeconds(80), TimeSpan.FromSeconds(82)),
		new SimulatedUtterance("S1", "Use reflection to list all methods of a type.", TimeSpan.FromSeconds(82), TimeSpan.FromSeconds(84)),
		new SimulatedUtterance("S1", "Demonstrate pattern matching using a switch expression.", TimeSpan.FromSeconds(84), TimeSpan.FromSeconds(86)),
		new SimulatedUtterance("S1", "Implement a record type and show immutability.", TimeSpan.FromSeconds(86), TimeSpan.FromSeconds(88)),
		new SimulatedUtterance("S1", "Use using statements to properly dispose of resources.", TimeSpan.FromSeconds(88), TimeSpan.FromSeconds(90)),
		new SimulatedUtterance("S1", "Create a Task that runs work in the background thread pool.", TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(92)),
		new SimulatedUtterance("S1", "Use Parallel.For to sum an array of numbers.", TimeSpan.FromSeconds(92), TimeSpan.FromSeconds(94)),
		new SimulatedUtterance("S1", "Lock a critical section to prevent race conditions.", TimeSpan.FromSeconds(94), TimeSpan.FromSeconds(96)),
		new SimulatedUtterance("S1", "Write a unit test that verifies a simple add method.", TimeSpan.FromSeconds(96), TimeSpan.FromSeconds(98)),
		new SimulatedUtterance("S1", "Explain how to log exceptions using ILogger in ASP.NET Core.", TimeSpan.FromSeconds(98), TimeSpan.FromSeconds(100)),
	};

	// New: Interrogative questions sample (1-25)
	public static IReadOnlyList<SimulatedUtterance> CSharpInterrogativeQuestions => new[]
	{
		new SimulatedUtterance("S1", "What is the difference between a class and a struct in C#?", TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(2)),
		new SimulatedUtterance("S1", "How does garbage collection work in the .NET runtime?", TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4)),
		new SimulatedUtterance("S1", "What is the difference between ref and out parameters?", TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(6)),
		new SimulatedUtterance("S1", "How is memory managed for value types versus reference types?", TimeSpan.FromSeconds(6), TimeSpan.FromSeconds(8)),
		new SimulatedUtterance("S1", "What is the purpose of the IDisposable interface?", TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(10)),
		new SimulatedUtterance("S1", "What does the async keyword actually do in C#?", TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(12)),
		new SimulatedUtterance("S1", "How do you handle exceptions in asynchronous methods?", TimeSpan.FromSeconds(12), TimeSpan.FromSeconds(14)),
		new SimulatedUtterance("S1", "What is the difference between IEnumerable and IQueryable?", TimeSpan.FromSeconds(14), TimeSpan.FromSeconds(16)),
		new SimulatedUtterance("S1", "What is dependency injection and why is it useful?", TimeSpan.FromSeconds(16), TimeSpan.FromSeconds(18)),
		new SimulatedUtterance("S1", "How does the using statement help manage resources?", TimeSpan.FromSeconds(18), TimeSpan.FromSeconds(20)),
		new SimulatedUtterance("S1", "What is the difference between Task and ValueTask?", TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(22)),
		new SimulatedUtterance("S1", "How does a lambda expression differ from an anonymous method?", TimeSpan.FromSeconds(22), TimeSpan.FromSeconds(24)),
		new SimulatedUtterance("S1", "What is the purpose of a delegate in C#?", TimeSpan.FromSeconds(24), TimeSpan.FromSeconds(26)),
		new SimulatedUtterance("S1", "How do events work under the hood in the CLR?", TimeSpan.FromSeconds(26), TimeSpan.FromSeconds(28)),
		new SimulatedUtterance("S1", "What is reflection used for in .NET?", TimeSpan.FromSeconds(28), TimeSpan.FromSeconds(30)),
		new SimulatedUtterance("S1", "How do records differ from classes?", TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(32)),
		new SimulatedUtterance("S1", "What are pattern matching expressions used for?", TimeSpan.FromSeconds(32), TimeSpan.FromSeconds(34)),
		new SimulatedUtterance("S1", "What is the difference between == and Equals()?", TimeSpan.FromSeconds(34), TimeSpan.FromSeconds(36)),
		new SimulatedUtterance("S1", "What is the role of the Common Language Runtime (CLR)?", TimeSpan.FromSeconds(36), TimeSpan.FromSeconds(38)),
		new SimulatedUtterance("S1", "How do you implement encapsulation in C#?", TimeSpan.FromSeconds(38), TimeSpan.FromSeconds(40)),
		new SimulatedUtterance("S1", "What are nullable reference types and why were they introduced?", TimeSpan.FromSeconds(40), TimeSpan.FromSeconds(42)),
		new SimulatedUtterance("S1", "How does the lock keyword prevent race conditions?", TimeSpan.FromSeconds(42), TimeSpan.FromSeconds(44)),
		new SimulatedUtterance("S1", "What is a cancellation token and how is it used?", TimeSpan.FromSeconds(44), TimeSpan.FromSeconds(46)),
		new SimulatedUtterance("S1", "What is the difference between string interpolation and concatenation?", TimeSpan.FromSeconds(46), TimeSpan.FromSeconds(48)),
		new SimulatedUtterance("S1", "How does inheritance support polymorphism in C#?", TimeSpan.FromSeconds(48), TimeSpan.FromSeconds(50)),
	};

	// New: Imperative / instructional commands (26-50)
	public static IReadOnlyList<SimulatedUtterance> CSharpImperativeCommands => new[]
	{
		new SimulatedUtterance("S1", "Create a class named Car with properties Make and Model.", TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(2)),
		new SimulatedUtterance("S1", "Implement a method that calculates the factorial of a number.", TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4)),
		new SimulatedUtterance("S1", "Write a loop that prints all even numbers from 1 to 100.", TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(6)),
		new SimulatedUtterance("S1", "Declare a list of integers and sort it in ascending order.", TimeSpan.FromSeconds(6), TimeSpan.FromSeconds(8)),
		new SimulatedUtterance("S1", "Define an interface IShape with a method GetArea().", TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(10)),
		new SimulatedUtterance("S1", "Implement the IShape interface in a Circle class.", TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(12)),
		new SimulatedUtterance("S1", "Write a constructor that initializes all fields of a class.", TimeSpan.FromSeconds(12), TimeSpan.FromSeconds(14)),
		new SimulatedUtterance("S1", "Use LINQ to filter all names that start with the letter A.", TimeSpan.FromSeconds(14), TimeSpan.FromSeconds(16)),
		new SimulatedUtterance("S1", "Serialize an object to JSON using System.Text.Json.", TimeSpan.FromSeconds(16), TimeSpan.FromSeconds(18)),
		new SimulatedUtterance("S1", "Deserialize a JSON string into a strongly typed object.", TimeSpan.FromSeconds(18), TimeSpan.FromSeconds(20)),
		new SimulatedUtterance("S1", "Create a try–catch block that handles a NullReferenceException.", TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(22)),
		new SimulatedUtterance("S1", "Implement a generic class that stores and retrieves items by key.", TimeSpan.FromSeconds(22), TimeSpan.FromSeconds(24)),
		new SimulatedUtterance("S1", "Write an extension method for strings that counts vowels.", TimeSpan.FromSeconds(24), TimeSpan.FromSeconds(26)),
		new SimulatedUtterance("S1", "Create an asynchronous method that downloads a web page.", TimeSpan.FromSeconds(26), TimeSpan.FromSeconds(28)),
		new SimulatedUtterance("S1", "Await a background task and print the result when complete.", TimeSpan.FromSeconds(28), TimeSpan.FromSeconds(30)),
		new SimulatedUtterance("S1", "Demonstrate dependency injection using IServiceCollection.", TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(32)),
		new SimulatedUtterance("S1", "Add a custom middleware component to an ASP.NET Core pipeline.", TimeSpan.FromSeconds(32), TimeSpan.FromSeconds(34)),
		new SimulatedUtterance("S1", "Write a unit test for a method that multiplies two numbers.", TimeSpan.FromSeconds(34), TimeSpan.FromSeconds(36)),
		new SimulatedUtterance("S1", "Use reflection to list all properties of a class.", TimeSpan.FromSeconds(36), TimeSpan.FromSeconds(38)),
		new SimulatedUtterance("S1", "Apply a custom attribute to mark deprecated methods.", TimeSpan.FromSeconds(38), TimeSpan.FromSeconds(40)),
		new SimulatedUtterance("S1", "Use pattern matching in a switch expression to handle types differently.", TimeSpan.FromSeconds(40), TimeSpan.FromSeconds(42)),
		new SimulatedUtterance("S1", "Implement a record type that represents a Point(x, y).", TimeSpan.FromSeconds(42), TimeSpan.FromSeconds(44)),
		new SimulatedUtterance("S1", "Write a LINQ query that joins two collections on a shared key.", TimeSpan.FromSeconds(44), TimeSpan.FromSeconds(46)),
		new SimulatedUtterance("S1", "Use Parallel.For to compute a sum across an array.", TimeSpan.FromSeconds(46), TimeSpan.FromSeconds(48)),
		new SimulatedUtterance("S1", "Implement exception logging using the built-in ILogger interface.", TimeSpan.FromSeconds(48), TimeSpan.FromSeconds(50)),
	};

	// Centralized expected question texts for scenarios.
	public static IReadOnlyList<string> StandupExpectedQuestions => new[]
	{
		"Can we finalize the sprint plan?",
		"What are the current error rates?",
		"How do we add tracing?",
		"It's stable, right?"
	};
	public static IReadOnlyList<string> SupportTicketExpectedQuestions => new[]
	{
		"Why is my account locked?",
		"Can you unlock it now?",
		"It's urgent, okay?"
	};
	public static IReadOnlyList<string> LowConfidenceOfQuestionExpectedQuestions => new[]
	{
		"Hey, what's the weather?"
	};
	public static IReadOnlyList<string> ImperativeRequestsExpectedQuestions => new[]
	{
		"Explain the difference between class and struct in C sharp.",
		"Explain dependency injection in .NET applications.",
		"Explain the difference between interface and abstract class."
	};
	public static IReadOnlyList<string> ImperativeCodingRequestsExpectedQuestions =>
		ImperativeCodingRequests.Select(u => u.Text).ToArray();

	public static IReadOnlyList<string> CSharpInterrogativeExpectedQuestions =>
		CSharpInterrogativeQuestions.Select(u => u.Text).ToArray();

	public static IReadOnlyList<string> CSharpImperativeExpectedQuestions =>
		CSharpImperativeCommands.Select(u => u.Text).ToArray();
}
