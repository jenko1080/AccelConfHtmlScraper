This HTML scraper fetches paper data from the IPAC Proceedings site for the years 2011 – 2019, as published online by JACoW. The proceedings are published under the Creative Commons Attribution 3.0 Licence, meaning you are free to share, copy and redistribute as well as remix, transform and build upon the material, providing it is attributed. This data scraper was not created or endorsed by the original publishers. This data scraper remains available under the same licencing.

The data is not cleaned or processed (much), at the time of writing the code outputs warnings to console to allow quality checking to ensure that all fields contain consistent attributes as described below.

The attributes collected include (see `Models/Paper.cs`):

- AuthorName: The name of the Author
- AuthorLocation: The place of work or research of the Author
- PaperID: The unique identifier for the publication for that year
- Year: The year of the conference
- PaperTitle: The title of the paper
- PaperAbstract: The abstract of the paper
- Category: The category that the paper was presented under at the conference
- Subcategory: The subcategory that the paper was presented under at the conference

The code can be configured to pull from URLs with the following pattern:

- http://accelconf.web.cern.ch/ipac{year}/html/class.htm

An extract as at 27/03/2021 is included in `papersJson.rar`
