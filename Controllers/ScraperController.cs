using HtmlAgilityPack;
using AccelConfHtmlScraper.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;

namespace AccelConfHtmlScraper.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ScraperController: ControllerBase
    {
        private readonly ILogger<ScraperController> _logger;

        public ScraperController(ILogger<ScraperController> logger)
        {
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> DoTheThing()
        {

            // For each year... get URLs
            var uris = GetUrisForRange(2011, 9);

            List<Paper> yearPapers = new List<Paper>();
            foreach (var uri in uris)
            {
                _logger.LogInformation($"Starting uri: {uri.Value.AbsoluteUri}");
                yearPapers.AddRange(await GetAllPaperDetails(uri));
            }

            using FileStream createStream = System.IO.File.Create("papersJson.json");
            await JsonSerializer.SerializeAsync(createStream, yearPapers);

            return Ok("THE FILE HAS BEEN WRITTEN!!!");
        }

        private async Task<List<Paper>> GetAllPaperDetails(KeyValuePair<int, Uri> paperYearKvp)
        {
            var papers = new List<Paper>();

            var url = paperYearKvp.Value;
            var html = await CallUrl(url);

            HtmlDocument htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(html);

            // get page body
            var listBody = htmlDoc.DocumentNode
                .ChildNodes
                .First(x => x.Name == "html")
                .ChildNodes
                .First(x => x.Name == "body");

            // get only the span and ul items
            var catsAndSubCatsList = listBody.ChildNodes.Where(node => node.Name == "span" || node.Name == "ul").ToList();
            for (int i = 0; i < catsAndSubCatsList.Count; )
            {
                // Category name is contained within the span
                var categoryName = HttpUtility.HtmlDecode(catsAndSubCatsList[i].InnerText);
                _logger.LogInformation($"-- Starting Cat: {categoryName}");

                // subcategories are the <a> within each <li>
                foreach (var subCatLi in catsAndSubCatsList[i+1].ChildNodes.Where(node => node.Name == "li"))
                {
                    // Get link and name from <a>
                    var subCatA = subCatLi.ChildNodes.First(node => node.Name == "a");

                    // Store link and name
                    var subCategoryName = subCatA.InnerHtml;
                    var subCategoryLink = subCatA.GetAttributeValue("href", "");
                    _logger.LogInformation($"---- Starting SubCat: {subCategoryName}");

                    var subWatch = System.Diagnostics.Stopwatch.StartNew();
                    // Get page for the subcategory with all the papers
                    var uriToFolder =
                        url.AbsoluteUri.Substring(0, url.AbsoluteUri.Length - url.Segments.Last().Length);
                    var subCategoryPageUrl = new Uri(uriToFolder + subCategoryLink);
                    var subCategoryPageContent = await CallUrl(subCategoryPageUrl);
                    HtmlDocument htmlDocSubCat = new HtmlDocument();
                    htmlDocSubCat.LoadHtml(subCategoryPageContent);
                    subWatch.Stop();
                    _logger.LogInformation($"------ SubCat Fetch: {subWatch.ElapsedMilliseconds} ms");

                    var paperTableRows = htmlDocSubCat.DocumentNode
                        .ChildNodes
                        .First(x => x.Name == "html")
                        .ChildNodes
                        .First(x => x.Name == "body")
                        .ChildNodes
                        .First(x => x.Name == "table")
                        .ChildNodes
                        .Where(x => x.Name == "tr")
                        .Skip(1)
                        .ToList();

                    for (int j = 0; j < paperTableRows.Count(); )
                    {
                        // First row is PaperId + Title
                        var idTitleRow = paperTableRows[j].ChildNodes.Where(x => x.Name == "td").ToList();
                        var paperId = idTitleRow[0].ChildNodes[0].InnerText;
                        var paperTitle = idTitleRow[1].InnerText;

                        if (string.IsNullOrWhiteSpace(paperId) || string.IsNullOrWhiteSpace(paperTitle))
                        {
                            _logger.LogError($"Paper ID or Title are null or whitespace: {paperId}: {paperTitle}");
                        }
                        else if (!Char.IsLetter(paperId[0]) || !Char.IsLetter(paperTitle[0]))
                        {
                            _logger.LogWarning($"Paper ID or Title start with a non-letter: {paperId}: {paperTitle}");
                        }

                        j++;

                        if (paperTableRows[j].ChildNodes.Where(x => x.Name == "td").Any(x => x.GetAttributeValue("class", "") == "papkey"))
                        {
                            j++;
                        }

                        // Second row is Authors, find all the LI items (one LI per place, multiple inner authors)
                        var paperAuthorLi = paperTableRows[j]
                            .ChildNodes.Where(x => x.Name == "td").ToList()[1]
                            .ChildNodes[0].ChildNodes.Where(x => x.Name == "li");

                        var authors = new List<PaperAuthor>();
                        foreach (var authorSection in paperAuthorLi)
                        {
                            var location = authorSection.ChildNodes.Last(x => x.Name == "#text").InnerText.Trim();
                            var authorsLi = authorSection.ChildNodes.First(x => x.Name == "span");

                            var primaryAuthorsHtml = authorsLi.ChildNodes
                                .Where(x => x.Name == "strong");

                            var primaryAuthors = primaryAuthorsHtml
                                .Select(x => new PaperAuthor
                                {
                                    AuthorName = HttpUtility.HtmlDecode(x.InnerText),
                                    AuthorPlace = location,
                                    IsPrimaryAuthor = true
                                });

                            authors.AddRange(primaryAuthors);

                            //if (authors.Any(x => x.AuthorName.Contains(",")))
                            //{
                            //    _logger.LogCritical("AHHHHHHHHHHH, PRIMARY AUTHOR NAME HAS A COMMA");
                            //}

                            var authorsText = authorsLi.InnerText;
                            var primaryAuthorsHtmlLength = primaryAuthorsHtml.Sum(x => x.InnerText.Length);
                            authorsText = authorsText.Substring(primaryAuthorsHtmlLength);
                            if (!string.IsNullOrWhiteSpace(authorsText))
                            {
                                var subAuthors = HttpUtility.HtmlDecode(authorsText).Substring(2).Split(",").Select(name =>
                                    new PaperAuthor
                                    {
                                        AuthorName = name.Trim(),
                                        AuthorPlace = location,
                                        IsPrimaryAuthor = false
                                    });

                                authors.AddRange(subAuthors);
                            }
                        }

                        //var paperAuthors = .InnerText;
                        j++;

                        // Third row is description... except when it's funding AND description
                        string paperDescr;

                        try
                        {
                            bool? first;
                            if (paperTableRows[j].ChildNodes.Where(x => x.Name == "td").ToList()[1].ChildNodes
                                .First(x => x.Name == "span").ChildNodes[0].InnerText.StartsWith("Funding:"))
                            {
                                paperDescr = paperTableRows[j].ChildNodes.Where(x => x.Name == "td").ToList()[1].ChildNodes.Where(x => x.Name == "span").ElementAt(1).InnerText;
                                first = true;
                            }
                            else
                            {
                                paperDescr = paperTableRows[j].ChildNodes.Where(x => x.Name == "td").ToList()[1].ChildNodes.First(x => x.Name == "span").InnerText;
                                first = false;
                            }

                            if (string.IsNullOrWhiteSpace(paperDescr))
                            {
                                _logger.LogError($"Paper description is null or white space: {paperId}: {paperTitle} ---- {paperDescr}");
                            }
                            else if (!Char.IsLetter(paperDescr[0]))
                            {
                                _logger.LogWarning($"Paper description starts with a non-letter: {paperId}: {paperTitle} ---- {paperDescr}");
                            }
                        }
                        catch (Exception e)
                        {
                            paperDescr = "";
                            _logger.LogCritical("Error fetching paper description");
                        }
                        j++;
                        
                        // Any row after is useless (until after sprr)
                        while (paperTableRows[j].GetAttributeValue("class", "") != "sprr")
                        {
                            j++;
                        }

                        foreach (var paperAuthor in authors)
                        {
                            papers.Add(new Paper
                            {
                                Year = paperYearKvp.Key,
                                Category = categoryName,
                                SubCategory = subCategoryName,
                                PaperId = paperId,
                                PaperName = paperTitle,
                                AuthorName = paperAuthor.AuthorName,
                                AuthorPlace = paperAuthor.AuthorPlace,
                                IsPrimaryAuthor = paperAuthor.IsPrimaryAuthor,
                                Description = paperDescr
                            });
                        }
                        
                        j++;
                    }
                }

                i = i + 2;
            }

            return papers;
        }


        private static async Task<string> CallUrl(Uri uri)
        {
            HttpClient client = new HttpClient();
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls13;
            client.DefaultRequestHeaders.Accept.Clear();
            var response = client.GetStringAsync(uri);
            return await response;
        }

        private Dictionary<int, Uri> GetUrisForRange(int start, int count)
        {
            var mainString = "http://accelconf.web.cern.ch/ipac{0}/html/class1.htm";

            var items = Enumerable.Range(start, count)
                .Select(x => new KeyValuePair<int, Uri>(x, new Uri(string.Format(mainString, x))));

            return new Dictionary<int, Uri>(items);
        }
    }
}
