using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CefSharp.OffScreen;
using CefSharp;
using System.Threading;
using System.Diagnostics;
using Newtonsoft.Json;
using System.IO;
using System.Dynamic;

namespace Cars
{
    internal class Program
    {
        const string url = "https://cars.com/signin/";
        const int defaultTimeout = 100000; // in milliseconds
        const string loginEmail = "johngerson808@gmail.com";
        const string loginPassword = "test8008";

        private static ChromiumWebBrowser browser;
        private static string pathForFiles;

        static void Main(string[] args)
        {
            try
            {
                #region Prepare Cef
                pathForFiles = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CarsCom");
                Directory.CreateDirectory(pathForFiles);
                Console.WriteLine($"Output directory for results: '{pathForFiles}'");

                Console.WriteLine("Initializing Cef...");
                Cef.InitializeAsync(new CefSettings { LogSeverity = LogSeverity.Error }).Wait();
                Console.WriteLine($"Cef initialized.");

                browser = new ChromiumWebBrowser();
                while (!browser.IsBrowserInitialized)
                    Thread.Sleep(0);
                Console.WriteLine("Browser initialized.");
                Console.WriteLine();
                #endregion

                #region Login, set filters and click search button
                article_1(); // Login.
                Console.WriteLine();

                article_2_3(); // Set filters and click search button.
                Console.WriteLine();
                #endregion

                #region Prepare results variable
                dynamic results = new ExpandoObject();
                results.ModelS = new ExpandoObject();
                results.ModelX = new ExpandoObject();
                #endregion

                #region Gather "Model S" data
                results.ModelS.AllCarsData = article_4(); // Gather all data for all cars on the first 2 pages.
                results.ModelS.SpecificCarData = article_5(); // Choose a specific car and gather specific car data.
                results.ModelS.HomeDeliveryData = article_6(); // Click "home delivery" and gather all data.
                Console.WriteLine();
                #endregion

                article_7(); // From the search results page click "Model X" to make checked it, than click "Model S" checkbox to uncheck it.
                Console.WriteLine();

                #region Gather "Model X" data
                results.ModelX.AllCarsData = article_4(); // Gather all data for all cars on the first 2 pages.
                results.ModelX.SpecificCarData = article_5(); // Choose a specific car and gather specific car data.
                results.ModelX.HomeDeliveryData = article_6(); // Click "home delivery" and gather all data.
                Console.WriteLine();
                #endregion

                #region Write data to file
                var fileName = Path.Combine(pathForFiles, $"CarsComData.json");
                File.WriteAllText(fileName, JsonConvert.SerializeObject(results, Formatting.Indented));
                Console.WriteLine($"All results written to file '{fileName}'");
                #endregion

                waitForBrowser();
                saveScreenShot();

                Console.WriteLine("Success.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {(ex.InnerException?.Message ?? ex.Message)}.");
            }

            Console.WriteLine("Press any key...");
            Console.ReadKey();
            browser?.Dispose();
            Cef.Shutdown();
        }

        /// <summary>
        /// Login.
        /// </summary>
        /// <exception cref="Exception"></exception>
        private static void article_1()
        {
            Console.WriteLine("Loading login page...");
            var taskComplete = browser.LoadUrlAsync(url).Wait(defaultTimeout);
            if (!taskComplete)
                throw new Exception("Timed out while loading login page");
            Console.WriteLine("Login page loaded.");

            //Login. Username: johngerson808@gmail.com Password: test8008
            waitForBrowser();
            Console.WriteLine("Trying to login...");
            var jsCode = $@"
                document.querySelector('#email').value = '{loginEmail}';
                document.querySelector('#password').value = '{loginPassword}';
                document.querySelector('button[type=submit]').click();";
            taskComplete = browser.EvaluateScriptAsync(jsCode).ContinueWith(jsResponse => {
                Console.WriteLine($"Login info was sent.");
            }).Wait(defaultTimeout);
            if (!taskComplete)
                throw new Exception("Timed out while login");

            // Check for our user name if we logged in successfully:
            waitForBrowser(100);
            Console.WriteLine("Login response received. Checking if login is successful...");
            jsCode = @"document.querySelector('.nav-user-name').innerHTML;";
            var username = "";
            taskComplete = browser.EvaluateScriptAsync(jsCode).ContinueWith(jsResponse => {
                username = ((string)jsResponse.Result.Result)?.Trim();
            }).Wait(defaultTimeout);
            if (!taskComplete)
                throw new Exception("Timed out while checking login");
            if (username != "Hi, john g")
                throw new Exception("Login failed");
            Console.WriteLine("Login successful.");
        }

        /// <summary>
        /// Set filters and click search button.
        /// </summary>
        /// <exception cref="Exception"></exception>
        private static void article_2_3()
        {
            // Set filters for search and click "Search" button:
            // make-model-search-stocktype -> "used"
            // makes -> "tesla"
            // models -> "tesla-model_s"
            // make-model-max-price -> "100000"
            // make-model-maximum-distance -> "all"
            // make-model-zip -> "94596"
            // data-linkname="search-used-make"
            Console.WriteLine("Setting filters and clicking 'Search' button...");
            var jsCode = @"
                let stockType = document.querySelector('#make-model-search-stocktype');
                stockType.value = 'used';
                stockType.dispatchEvent(new Event('change', { 'bubbles': true }));
                let makes = document.querySelector('#makes');
                makes.value = 'tesla';
                makes.dispatchEvent(new Event('change', { 'bubbles': true }));
                document.querySelector('#models').value = 'tesla-model_s';
                document.querySelector('#make-model-max-price').value = '100000';
                document.querySelector('#make-model-maximum-distance').value = 'all';
                document.querySelector('#make-model-zip').value = '94596';
                document.querySelector('button[type=submit][data-linkname=search-used-make]').click();
            ";
            waitForBrowser();
            var taskComplete = browser.EvaluateScriptAsync(jsCode).Wait(defaultTimeout);
            if (!taskComplete)
                throw new Exception("Timed out while search was starting");
        }

        /// <summary>
        /// Gather all data for all cars on the first 2 pages.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private static List<object> article_4()
        {
            // Gather all cars on the first page of results:
            Console.WriteLine("Gathering all cars from first 2 pages of results...");

            var carList = gatherAllCarsDataInPage();
            if (carList == null)
                throw new Exception("No cars returned.");

            // Click "Next" button to go to next page:
            var jsCode = @"
                function clickNextPageButton() {
                    let nextPageButton = document.querySelector('#next_paginate');
                    nextPageButton.click();
                }

                clickNextPageButton();
            ";
            waitForBrowser();
            var taskComplete = browser.EvaluateScriptAsync(jsCode).Wait(defaultTimeout);
            if (!taskComplete)
                throw new Exception("Timed out while going to next page");

            // Gather all cars on the second page of results if any and append them to our list:
            var secondPage = gatherAllCarsDataInPage();
            if (secondPage != null)
                carList.AddRange(secondPage);

            return carList;
        }

        /// <summary>
        /// Choose a specific car (that has "Home Delivery" info if any or first car in the list) and gather that specific car data.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private static object article_5()
        {
            // Choose a specific car:
            waitForBrowser();
            Console.WriteLine($"Choosing a specific car and clicking on it...");
            var jsCode = @"
                function clickSpecificCar() {
                    let container = document.querySelector('#vehicle-cards-container');
                    let specificCar = container.querySelector(""div.vehicle-details > div.vehicle-badging[data-contents*='\""home_delivery_badge\"":true']"")?.parentElement?.querySelector('a.vehicle-card-link');
                    if (!specificCar)
                        specificCar = container.querySelector('a.vehicle-card-link');
                    specificCar.click();
                }

                clickSpecificCar();
            ";
            var taskComplete = browser.EvaluateScriptAsync(jsCode).Wait(defaultTimeout);
            if (!taskComplete)
                throw new Exception("Timed out while going to detail of a car");

            // Gather specific car data:
            waitForBrowser();
            Console.WriteLine($"Gathering specific car data...");
            jsCode = @"
                function gatherSpecificCarData() {
                    let car = {};
                    car.title = document.querySelector('div.title-section > h1.listing-title').innerHTML;
                    car.mileage = document.querySelector('div.title-section > div.listing-mileage').innerHTML?.trim();
                    car.primaryPrice = document.querySelector('div.price-section > span.primary-price').innerHTML;
                    car.secondaryPrice = document.querySelector('div.price-section > span.secondary-price')?.innerHTML;
                    
                    let sections = document.querySelectorAll('div.basics-content-wrapper > section.sds-page-section');
                    for (let i = 0; i < 3; i++) {
                        let section = car[sections[i].querySelector('h2').innerHTML] = {};
                        let propNameList = sections[i].querySelectorAll('dl > dt');
                        let propValueList = sections[i].querySelectorAll('dl > dd');
                        for(let j=0;j<propNameList.length;j++){
                            let propName = propNameList[j].innerHTML.trim();
                            let propValue = [...propValueList[j].querySelectorAll('li')].map(x => x.innerHTML).join(' | ');
                            if (propValue == '')
                                propValue = propValueList[j].innerHTML.trim();
                            section[propName] = propValue;
                        }
                    }
                    return car;
                }

                gatherSpecificCarData();
            ";

            object result = null;
            taskComplete = browser.EvaluateScriptAsync(jsCode).ContinueWith(jsResponse =>
            {
                if (!jsResponse.Result.Success)
                    throw new Exception(jsResponse.Result.Message);
                result = jsResponse.Result.Result;
            }).Wait(defaultTimeout);
            if (!taskComplete)
                throw new Exception("Timed out while gathering detail of a car");

            return result;
        }

        /// <summary>
        /// Click "Home Delivery" button and gather all data.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private static object article_6()
        {
            // Click "home delivery":
            waitForBrowser();
            Console.WriteLine($"Clicking 'home delivery' button...");
            var jsCode = @"
                function clickHomeDeliveryButton() {
                    let homeDelivery = document.querySelector('.sds-badge--home-delivery');
                    homeDelivery.click();
                }

                clickHomeDeliveryButton();
            ";
            var taskComplete = browser.EvaluateScriptAsync(jsCode).Wait(defaultTimeout);
            if (!taskComplete)
                throw new Exception($"Timed out while clicking 'home delivery'");

            // Gather 'home delivery' data:
            waitForBrowser();
            Console.WriteLine($"Gathering 'home delivery' data...");
            jsCode = @"
                function gatherHomeDeliveryData() {
                    let result = [];
                    let highlights = document.querySelectorAll('#sds-modal li');
                    for (let i = 0; i < highlights.length; i++) {
                        if(highlights[i].innerHTML.trim() == '')
                            continue;
                        let label = highlights[i].querySelector('.sds-badge__label').innerText;
                        let value = highlights[i].querySelector('.badge-description').innerText;
                        let item = {};
                        item[label] = value;
                        result.push(item);
                    }
                    return result;
                }

                gatherHomeDeliveryData();
            ";

            object result = null;
            taskComplete = browser.EvaluateScriptAsync(jsCode).ContinueWith(jsResponse =>
            {
                if (!jsResponse.Result.Success)
                    throw new Exception(jsResponse.Result.Message);
                result = jsResponse.Result.Result;
            }).Wait(defaultTimeout);
            if (!taskComplete)
                throw new Exception("Timed out while gathering home delivery data");

            return result;
        }

        /// <summary>
        /// Go back to search results page, click "Model X" to make checked it, than click "Model S" checkbox to uncheck it.
        /// </summary>
        /// <exception cref="Exception"></exception>
        private static void article_7()
        {
            // Click "Search Results" to go back:
            waitForBrowser();
            Console.WriteLine("Clicking 'Search Results' to go back...");
            var jsCode = @"
                let searchResults = document.querySelector('ul.sds-breadcrumb > li.sds-breadcrumb__parent a');
                searchResults.click();
            ";
            var taskComplete = browser.EvaluateScriptAsync(jsCode).Wait(defaultTimeout);
            if (!taskComplete)
                throw new Exception("Timed out while clicking 'Search Results' to go back");


            // Click "Model X":
            waitForBrowser();
            Console.WriteLine("Clicking 'Model X' checkbox...");
            jsCode = @"
                let modelXCheckBox = document.querySelector('input#model_tesla-model_x');
                modelXCheckBox.click();
            ";
            taskComplete = browser.EvaluateScriptAsync(jsCode).Wait(defaultTimeout);
            if (!taskComplete)
                throw new Exception("Timed out while clicking 'Model X' checkbox");

            // Click "Model S" to uncheck it:
            waitForBrowser();
            Console.WriteLine("Clicking 'Model S' checkbox to uncheck it...");
            jsCode = @"
                let modelSCheckBox = document.querySelector('input#model_tesla-model_s');
                modelSCheckBox.click();
            ";
            taskComplete = browser.EvaluateScriptAsync(jsCode).Wait(defaultTimeout);
            if (!taskComplete)
                throw new Exception("Timed out while clicking 'Model S' checkbox to uncheck");
        }

        /// <summary>
        /// Gather all cars data in current page.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private static List<object> gatherAllCarsDataInPage()
        {
            List<object> carList = null;

            var jsCode = @"
                function gatherAllCarsDataInPage() {
                    let container = document.querySelector('#vehicle-cards-container');
                    let cards = container.querySelectorAll('div.vehicle-card');
                    let result = [];
                    for(let i=0;i<cards.length;i++){
                        let car = {};
                        car.title = cards[i].querySelector('a.vehicle-card-link > h2')?.innerHTML;
                        car.mileage = cards[i].querySelector('div.mileage')?.innerHTML;
                        car.price = cards[i].querySelector('span.primary-price')?.innerHTML;
                        car.monthly = cards[i].querySelector('span.js-estimated-monthly-payment-formatted-value-with-abr')?.innerText;
                        car.dealer = cards[i].querySelector('div.dealer-name')?.innerText;
                        car.seller = cards[i].querySelector('div.seller-name')?.innerText;
                        car.milesfrom = cards[i].querySelector('div.miles-from')?.innerHTML.trim();
                        result.push(car);
                    }
                    return result;
                }

                gatherAllCarsDataInPage();
            ";
            waitForBrowser();
            var taskComplete = browser.EvaluateScriptAsync(jsCode).ContinueWith(jsResponse =>
            {
                if (!jsResponse.Result.Success)
                    throw new Exception(jsResponse.Result.Message);
                carList = (List<object>)jsResponse.Result.Result;
            }).Wait(defaultTimeout);
            if (!taskComplete)
                throw new Exception("Timed out while gathering cars data in current page");

            return carList;
        }

        /// <summary>
        /// Wait for browser to start loading and complete loading process.
        /// </summary>
        /// <param name="timeout"></param>
        /// <param name="loadTimeout"></param>
        /// <exception cref="Exception"></exception>
        private static void waitForBrowser(long timeout = 1000, long loadTimeout = 20000)
        {
            var sw = new Stopwatch();
            var loadingProgress = false;

            while (true)
            {
                // Wait for browser to start loading for specified timeout:
                sw.Start();
                while (!browser.IsLoading && sw.ElapsedMilliseconds < timeout)
                    Thread.Sleep(0);

                if (!browser.IsLoading) // It's timed out, it means there is nothing to load anymore.
                {
                    if (loadingProgress)
                        Console.WriteLine("Ok.");
                    return;
                }

                if (!loadingProgress)
                {
                    loadingProgress = true;
                    Console.Write("Loading...");
                }

                // Loading started, wait for browser to complete loading. It's unexpected to reach time out while loading:
                sw.Restart();
                var swProgress = new Stopwatch();
                swProgress.Start();
                while (browser.IsLoading && sw.ElapsedMilliseconds < loadTimeout)
                {
                    if (swProgress.ElapsedMilliseconds > 300)
                    {
                        Console.Write('.');
                        swProgress.Restart();
                    }
                    Thread.Sleep(0);
                }

                if (browser.IsLoading) // It's timed out. It's unexpected.
                    throw new Exception("Timed out while browser is loading");

                sw.Stop();
                sw.Reset();
            }
        }

        private static void saveScreenShot(string name = "")
        {
            var bmp = browser.ScreenshotOrNull();
            bmp.Save(Path.Combine(pathForFiles, $"{DateTime.Now:yyyyMMdd_HHmmss}{name}.bmp"));
            bmp.Dispose();
            bmp = null;
        }
    }
}
