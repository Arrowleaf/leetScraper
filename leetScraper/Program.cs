using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace leetScraper
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Base URL of the website to scrape
            string baseUrl = "http://books.toscrape.com//";

            // Folder to save scraped pages
            string folderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ScrapedPages");

            // Create the folder if it doesn't exist
            Directory.CreateDirectory(folderPath);

            try
            {
                // Create HttpClient
                using (var httpClient = new HttpClient())
                {
                    // Fetch the home page
                    var homePageHtml = await httpClient.GetStringAsync(baseUrl);
                    var homePageDocument = new HtmlDocument();
                    homePageDocument.LoadHtml(homePageHtml);

                    await SaveHtmlAsync(folderPath, "Index.html", homePageHtml, baseUrl, "");

                    // Display available categories
                    Console.WriteLine("Available Categories:");

                    // Fetch all categories and their URLs
                    Dictionary<string, string> categories = new Dictionary<string, string>();
                    var categoryNodes = homePageDocument.DocumentNode.SelectNodes("//ul[@class='nav nav-list']//ul/li/a");
                    if (categoryNodes != null)
                    {
                        foreach (var categoryNode in categoryNodes)
                        {
                            string categoryName = categoryNode.InnerText.Trim();
                            string categoryUrl = baseUrl + categoryNode.GetAttributeValue("href", "");
                            categories.Add(categoryName, categoryUrl);
                        }
                    }

                    string selectedCategory = DisplayCategories(categories);

                    // Check if the user wants to exit
                    if (selectedCategory.ToLower() == "exit")
                    {
                        return;
                    }

                    // Parse user input
                    if (!int.TryParse(selectedCategory, out int categoryChoice) || categoryChoice < 1 || categoryChoice > categories.Count)
                    {
                        Console.WriteLine("Invalid category number selected.");
                        return;
                    }

                    var selectedCategoryName = categories.Keys.ElementAt(categoryChoice - 1);
                    var selectedCategoryUrl = categories.Values.ElementAt(categoryChoice - 1);
                    var selectedCategoryHtml = await httpClient.GetStringAsync(selectedCategoryUrl);

                    // Extract the folder name from the category URL
                    var parts = selectedCategoryUrl.Split('/');
                    var categoryFolder = parts[parts.Length - 2]; // Get the second-to-last part

                    // Save the category page with the correct folder name
                    await SaveHtmlAsync(folderPath, "Index.html", selectedCategoryHtml, baseUrl, categoryFolder);

                    var selectedCategoryHtmlDocument = new HtmlDocument();
                    selectedCategoryHtmlDocument.LoadHtml(selectedCategoryHtml);

                    // Fetch book titles and URLs
                    Dictionary<string, string> books = new Dictionary<string, string>();
                    var bookNodes = selectedCategoryHtmlDocument.DocumentNode.SelectNodes("//article[@class='product_pod']");
                    if (bookNodes != null)
                    {
                        foreach (var bookNode in bookNodes)
                        {
                            var bookTitleNode = bookNode.SelectSingleNode(".//h3/a");
                            if (bookTitleNode != null)
                            {
                                string bookTitle = bookTitleNode.InnerText.Trim();
                                string bookUrl = baseUrl + bookTitleNode.GetAttributeValue("href", "");
                                books.Add(bookTitle, bookUrl);
                            }
                        }
                    }

                    string selectedBook = DisplayBooksInCategory(selectedCategoryName, books);

                    // Check if the user wants to go back
                    if (selectedBook.ToLower() == "back")
                    {
                        // Go back to the main menu if on the category page
                        Main(args).Wait();
                        return;
                    }

                    // Parse user input
                    if (!int.TryParse(selectedBook, out int bookChoice) || bookChoice < 1 || bookChoice > books.Count)
                    {
                        Console.WriteLine("Invalid book number selected.");
                        return;
                    }

                    await SaveBook(baseUrl, folderPath, httpClient, books, bookChoice, categoryFolder);
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"An error occurred while fetching data from the website: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                Console.WriteLine("Press Enter to continue...");
                Console.ReadLine();
            }
        }

        private static string DisplayCategories(Dictionary<string, string> categories)
        {
            // Display categories to the user
            int categoryIndex = 1;
            foreach (var category in categories)
            {
                Console.WriteLine($"{categoryIndex}. {category.Key}");
                categoryIndex++;
            }

            // Ask the user to select a category
            Console.WriteLine("\nEnter the number of the category you want to explore (Type 'exit' to quit):");
            string selectedCategory = Console.ReadLine();
            return selectedCategory;
        }

        private static string DisplayBooksInCategory(string selectedCategoryName, Dictionary<string, string> books)
        {
            // Display books to the user
            Console.WriteLine($"\nBooks in '{selectedCategoryName}':");
            int bookIndex = 1;
            foreach (var book in books)
            {
                Console.WriteLine($"{bookIndex}. {book.Key}");
                bookIndex++;
            }

            // Ask the user to select a book or go back
            Console.WriteLine("Type 'back' to go back to categories.");
            Console.Write("Enter the number of the book you want to view: ");
            string selectedBook = Console.ReadLine();
            return selectedBook;
        }

        private static async Task SaveBook(string baseUrl, string folderPath, HttpClient httpClient, Dictionary<string, string> books, int bookChoice, string categoryFolder)
        {
            try
            {
                // Display the URL of the selected book
                string selectedBookTitle = books.Keys.ElementAt(bookChoice - 1);
                string selectedBookUrl = books.Values.ElementAt(bookChoice - 1);

                // Fetch and save the HTML content of the selected book page
                var selectedBookHtml = await httpClient.GetStringAsync(selectedBookUrl);
                await SaveHtmlAsync(folderPath, $"{selectedBookTitle}.html", selectedBookHtml, baseUrl, categoryFolder);

                // Display the URL of the selected book
                Console.WriteLine($"\nURL of '{selectedBookTitle}': {selectedBookUrl}");

                // Open the book page in the default web browser
                System.Diagnostics.Process.Start(selectedBookUrl);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                Console.WriteLine("Press Enter to continue...");
                Console.ReadLine();
            }
        }

        static async Task SaveHtmlAsync(string folderPath, string fileName, string htmlContent, string baseUrl, string subfolderPath = "")
        {
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            if (!string.IsNullOrEmpty(subfolderPath))
            {
                folderPath = Path.Combine(folderPath, subfolderPath);
                Directory.CreateDirectory(folderPath);
            }

            string filePath = Path.Combine(folderPath, fileName);

            htmlContent = await ModifyHtmlContentAsync(htmlContent, baseUrl, folderPath, subfolderPath);

            await File.WriteAllTextAsync(filePath, htmlContent);
            Console.WriteLine($"Saved HTML to: {filePath}");
        }

        static async Task<string> ModifyHtmlContentAsync(string htmlContent, string baseUrl, string folderPath, string categoryFolder)
        {
            var htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(htmlContent);

            static void EnsureFolderExists(string folderPath)
            {
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }
            }

            var imagesFolderPath = Path.Combine(folderPath, "images", categoryFolder);
            EnsureFolderExists(imagesFolderPath);

            var stylesFolderPath = Path.Combine(folderPath, "styles", categoryFolder);
            EnsureFolderExists(stylesFolderPath);

            var imgNodes = htmlDocument.DocumentNode.SelectNodes("//img");
            if (imgNodes != null)
            {
                using (var httpClient = new HttpClient())
                {
                    foreach (var imgNode in imgNodes)
                    {
                        var src = imgNode.GetAttributeValue("src", "");
                        if (!string.IsNullOrWhiteSpace(src))
                        {
                            if (!src.StartsWith("http"))
                            {
                                src = new Uri(new Uri(baseUrl), src).AbsoluteUri;
                            }

                            var imageFileName = Path.GetFileName(src);
                            var localImagePath = Path.Combine(imagesFolderPath, imageFileName);
                            try
                            {
                                var imageBytes = await httpClient.GetByteArrayAsync(src);
                                File.WriteAllBytes(localImagePath, imageBytes);

                                imgNode.SetAttributeValue("src", Path.Combine("images", categoryFolder, imageFileName));
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Failed to download image '{imageFileName}': {ex.Message}");
                            }
                        }
                    }
                }
            }

            var linkNodes = htmlDocument.DocumentNode.SelectNodes("//link");
            if (linkNodes != null)
            {
                using (var httpClient = new HttpClient())
                {
                    foreach (var linkNode in linkNodes)
                    {
                        var href = linkNode.GetAttributeValue("href", "");
                        if (!string.IsNullOrWhiteSpace(href))
                        {
                            if (!href.StartsWith("http"))
                            {
                                href = new Uri(new Uri(baseUrl), href).AbsoluteUri;
                            }

                            if (href.EndsWith(".ico"))
                            {
                                continue;
                            }

                            var resourceFileName = Path.GetFileName(href);
                            var localResourcePath = Path.Combine(stylesFolderPath, resourceFileName);
                            try
                            {
                                var resourceContent = await httpClient.GetStringAsync(href);
                                File.WriteAllText(localResourcePath, resourceContent);

                                linkNode.SetAttributeValue("href", Path.Combine("styles", categoryFolder, resourceFileName));
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Failed to download resource '{resourceFileName}': {ex.Message}");
                            }
                        }
                    }
                }
            }

            return htmlDocument.DocumentNode.OuterHtml;
        }
    }
}
