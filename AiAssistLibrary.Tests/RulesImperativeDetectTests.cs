using System;
using System.Linq;
using System.Collections.Generic;
using AiAssistLibrary.Services.QuestionDetection;
using Xunit;

public sealed class RulesImperativeDetectTests
{
	private readonly RulesImperativeDetect _detector = new();

	[Fact]
	public void Detects_All_ImperativeCodingRequests_As_Questions()
	{
		var sentences = new[]
		{
			"Define a variable of type int and assign it a value.",
			"Declare a constant called Pi with the value3.14.",
			"Create a method that prints \"Hello World\" to the console.",
			"Write a method that adds two integers and returns the result.",
			"Write a function that takes a string and returns its length.",
			"Create a for loop that counts from one to ten.",
			"Use a while loop to print numbers from one to five.",
			"Create an if statement that checks whether a number is even.",
			"Implement a switch statement for the days of the week.",
			"Write a try–catch block that handles a divide-by-zero exception.",
			"Define a class named Person with properties Name and Age.",
			"Instantiate an object of the Person class and print its name.",
			"Create a constructor that initializes both Name and Age.",
			"Add a method to the Person class that returns a greeting message.",
			"Inherit from the Person class to create an Employee subclass.",
			"Override the greeting method in the Employee class.",
			"Declare an interface called IVehicle with a StartEngine method.",
			"Implement the IVehicle interface in a Car class.",
			"Create a static method that calculates factorial recursively.",
			"Write an extension method for the string type that reverses text.",
			"Use LINQ to select all even numbers from an integer list.",
			"Create a generic method that swaps two variables.",
			"Implement a List<string> and add three names to it.",
			"Sort a list of integers in descending order.",
			"Remove duplicate elements from a list using LINQ.",
			"Write a lambda expression that multiplies two numbers.",
			"Use the Action delegate to point to a void method.",
			"Use the Func<int,int,int> delegate to perform addition.",
			"Subscribe to and raise a custom event in C#.",
			"Create an asynchronous method that fetches data using HttpClient.",
			"Await an asynchronous call inside a try–catch block.",
			"Demonstrate how to cancel an asynchronous operation.",
			"Write code that reads text from a file using StreamReader.",
			"Write code that writes text to a file using StreamWriter.",
			"Serialize an object to JSON using System.Text.Json.",
			"Deserialize JSON back into an object.",
			"Demonstrate dependency injection with a simple service class.",
			"Register a singleton service in an ASP.NET Core application.",
			"Add custom middleware to log incoming HTTP requests.",
			"Configure routing for a minimal API endpoint /hello.",
			"Create and apply a custom attribute to a class.",
			"Use reflection to list all methods of a type.",
			"Demonstrate pattern matching using a switch expression.",
			"Implement a record type and show immutability.",
			"Use using statements to properly dispose of resources.",
			"Create a Task that runs work in the background thread pool.",
			"Use Parallel.For to sum an array of numbers.",
			"Lock a critical section to prevent race conditions.",
			"Write a unit test that verifies a simple add method.",
			"Explain how to log exceptions using ILogger in ASP.NET Core."
		};

		var failures = new List<string>();

		for (var i = 0; i < sentences.Length; i++)
		{
			var sentence = sentences[i];
			var qs = _detector.Detect(sentence, TimeSpan.Zero, TimeSpan.FromSeconds(2));

			// We expect at least one detection and that at least one detection is categorized as "Imperative".
			if (qs.Count == 0 || !qs.Any(q => string.Equals(q.Category, "Imperative", StringComparison.Ordinal)))
			{
				failures.Add($"#{i + 1}: '{sentence}' => detections={qs.Count}");
			}
		}

		if (failures.Count > 0)
		{
			// Provide detailed failure output to make it easy to identify which sentences failed.
			var msg = "The following sentences did not get classified as Imperative:\n" + string.Join("\n", failures);
			Assert.True(false, msg);
		}

		// Also assert that the total number of sentences tested matches expectation.
		Assert.Equal(50, sentences.Length);
	}

	[Theory]
	[InlineData("Explain dependency injection in .NET applications.")]
	[InlineData("Show me how to add tracing.")]
	[InlineData("Walk me through creating a minimal API.")]
	public void Categorizes_As_Imperative(string text)
	{
		var qs = _detector.Detect(text, TimeSpan.Zero, TimeSpan.FromSeconds(2));
		Assert.Single(qs);
		Assert.Equal("Imperative", qs[0].Category);
		Assert.True(qs[0].Confidence >= 0.75);
	}
}
