﻿using Correios.NET.Exceptions;
using Correios.NET.Extensions;
using Correios.NET.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using AngleSharp;
using AngleSharp.Html.Parser;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using System.Text.RegularExpressions;
using System.Globalization;

namespace Correios.NET
{
    public class Parser
    {
        #region Address

        /// <summary>
        /// Parse and converts the html page in a zip address
        /// </summary>
        /// <param name="html">html page</param>
        /// <returns>An Address</returns>
        /// <exception cref="Correios.NET.Exceptions.ParseException"></exception>
        public static IEnumerable<Address> ParseAddresses(string html)
        {
            //var config = Configuration.Default;
            //var context = BrowsingContext.New(config);
            //var document = context.OpenAsync(req => req.Content(html)).Result;            

            var document = new HtmlParser().ParseDocument(html);

            var content = document.QuerySelector("div.ctrlcontent");
            var responseText = content.QuerySelector("p").Text();

            if (responseText == "DADOS NAO ENCONTRADOS")
                throw new ParseException("Endereço não encontrado.");

            var list = new List<Address>();

            var tableRows = content.QuerySelectorAll("> table.tmptabela > tbody > tr").Skip(1);

            if (tableRows.Count() == 0)
                throw new ParseException("Endereço não encontrado.");

            foreach (var row in tableRows)
            {
                var address = row.Children;
                var street = address[0].Text().RemoveLineEndings();
                var district = address[1].Text().RemoveLineEndings();
                var cityState = address[2].Text().RemoveLineEndings().Split(new[] { '/' });

                if (cityState.Length != 2)
                    throw new ParseException("Não foi possível extrair as informações de Cidade e Estado.");

                var city = cityState[0].Trim();
                var state = cityState[1].Trim();
                var zipcode = address[3].Text().RemoveHyphens();

                list.Add(new Address
                {
                    Street = street,
                    ZipCode = zipcode,
                    District = district,
                    City = city,
                    State = state
                });
            }


            return list;
        }

        #endregion

        #region Package

        /// <summary>
        /// Parse and converts the html page in a package
        /// </summary>
        /// <param name="html">html page</param>
        /// <returns>A Package</returns>
        /// <exception cref="Correios.NET.Exceptions.ParseException"></exception>
        public static Package ParsePackage(string html)
        {
            var document = new HtmlParser().ParseDocument(html);
            var packageCode = ParsePackageCode(document);
            var package = new Package(packageCode);
            package.AddTrackingInfo(ParsePackageTracking(document));
            return package;
        }

        private static string ParsePackageCode(IHtmlDocument document)
        {
            var code = string.Empty;
            var resultTitle = document.QuerySelector("body > p").Text();

            if (!string.IsNullOrEmpty(resultTitle) && resultTitle.Contains("-"))
                code = resultTitle.Split('-')[0].Trim();

            if (string.IsNullOrEmpty(code))
                throw new ParseException("Código da encomenda/pacote não foi encontrado.");

            return code;
        }

        private static IEnumerable<PackageTracking> ParsePackageTracking(IHtmlDocument document)
        {
            var tracking = new List<PackageTracking>();

            PackageTracking status = null;
            var tableRows = document.QuerySelectorAll("table tr");
            if (tableRows.Length == 0)
                throw new ParseException(document.QuerySelector("p").Text().RemoveLineEndings());

            try
            {
                foreach (var columns in tableRows.Skip(1).Select(tableRow => tableRow.Children))
                {
                    if (columns.Count() == 3)
                    {
                        status = new PackageTracking();
                        if (columns[0].HasAttribute("rowspan"))
                        {
                            status.Date = DateTime.Parse(columns[0].Text().RemoveLineEndings());
                        }

                        status.Location = columns[1].Text().RemoveLineEndings();
                        status.Status = columns[2].Text().RemoveLineEndings();

                        tracking.Add(status);
                    }
                    else
                    {
                        if (status != null)
                            status.Details = columns[0].Text().RemoveLineEndings();
                    }
                }
            }
            catch (Exception ex)
            {
                throw new ParseException("Não foi possível converter o pacote/encomenda.", ex);
            }

            if (tracking.Count() == 0)
                throw new ParseException("Rastreamento não encontrado.");

            return tracking;
        }

        #endregion

        #region Delivery Prices


        public static DeliveryPrice ParseDeliveryPrices(string mode, string html)
        {
            var document = new HtmlParser().ParseDocument(html);

            var content = document.QuerySelector("div.ctrlcontent");

            var error = document.QuerySelectorAll(".info.error").ToList();

            if (error.Count > 0)
                throw new ParseException("Não foi possível calcular o preço de entrega.", error.Select(x => x.Text()).ToArray());



            var tableRows = content.QuerySelectorAll("table.comparaResult tr.destaque td").ToList();



            Regex rgx = new Regex(@"\d+");


            var termText = tableRows[0].Text();
            var priceText = tableRows[1].Text();

            var match = rgx.Match(termText);


            decimal price;


            decimal.TryParse(priceText, NumberStyles.Currency, new CultureInfo("pt-BR"), out price);


            var deliveryPrice = new DeliveryPrice();

            deliveryPrice.Mode = mode;
            deliveryPrice.Days = match.Success ? int.Parse(match.Value) : -1;
            deliveryPrice.Price = price;


            return deliveryPrice;


        }

        #endregion
    }
}
