﻿using System;
using System.Collections.Generic;
using System.Linq;
 using Microsoft.AspNetCore.Mvc;
 using ShipIt.Exceptions;
using ShipIt.Models.ApiModels;
using ShipIt.Repositories;

namespace ShipIt.Controllers
{   
    [Route("orders/outbound")]

    public class Trucks
    {
        public int TruckCount { get; set; }
    }

    public class OutboundOrderController : ControllerBase
    {
        private static readonly log4net.ILog Log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType);

        private readonly IStockRepository _stockRepository;
        private readonly IProductRepository _productRepository;

        public OutboundOrderController(IStockRepository stockRepository, IProductRepository productRepository)
        {
            _stockRepository = stockRepository;
            _productRepository = productRepository;
        }

        [HttpPost("")]
        public int Post([FromBody] OutboundOrderRequestModel request) 
        {
            Log.Info(String.Format("Processing outbound order: {0}", request));

            var gtins = new List<String>();
            foreach (var orderLine in request.OrderLines)
            {
                if (gtins.Contains(orderLine.gtin))
                {
                    throw new ValidationException(String.Format("Outbound order request contains duplicate product gtin: {0}", orderLine.gtin));
                }
                gtins.Add(orderLine.gtin);
            }

            var productDataModels = _productRepository.GetProductsByGtin(gtins);
            var products = productDataModels.ToDictionary(p => p.Gtin, p => new Product(p));

            var lineItems = new List<StockAlteration>();
            var productIds = new List<int>();
            var errors = new List<string>();

            //total weight add
            float totalWeight = 0;

            foreach (var orderLine in request.OrderLines)
            {
                if (!products.ContainsKey(orderLine.gtin))
                {
                    errors.Add(string.Format("Unknown product gtin: {0}", orderLine.gtin));
                }
                else
                {
                    // are we calculating totalweight before additional stock addedd / dismissed due to lack of stock?
                    var product = products[orderLine.gtin];
                    totalWeight += product.Weight * orderLine.quantity; 
                    lineItems.Add(new StockAlteration(product.Id, orderLine.quantity));
                    productIds.Add(product.Id);
                }
            }

            if (errors.Count > 0)
            {
                throw new NoSuchEntityException(string.Join("; ", errors));
            }

            var stock = _stockRepository.GetStockByWarehouseAndProductIds(request.WarehouseId, productIds);

            var orderLines = request.OrderLines.ToList();
            errors = new List<string>();

            for (int i = 0; i < lineItems.Count; i++)
            {
                var lineItem = lineItems[i];
                var orderLine = orderLines[i];

                if (!stock.ContainsKey(lineItem.ProductId))
                {
                    errors.Add(string.Format("Product: {0}, no stock held", orderLine.gtin));
                    continue;
                }

                var item = stock[lineItem.ProductId];
                if (lineItem.Quantity > item.held)
                {
                    errors.Add(
                        string.Format("Product: {0}, stock held: {1}, stock to remove: {2}", orderLine.gtin, item.held,
                            lineItem.Quantity));
                }
            }

            if (errors.Count > 0)
            {
                throw new InsufficientStockException(string.Join("; ", errors));
            }

            _stockRepository.RemoveStock(request.WarehouseId, lineItems);

            int totalTrucks = (int)Math.Ceiling((decimal)(totalWeight)/2000);


            //     We don’t mind too much what the format of the new data is (our front end team will work with whatever you provide) but it should be clear;

            //     How many trucks we will need to process the order

            //     What items should be contained in each truck

            //     The total weight of the items in each truck

            //     As for how the items should be divided between the trucks, again we’ll leave it in part up to you - but please ensure that the following remains true

            //     No truck is assigned more than 2000kg in total.

            //     (where possible given the above) orders of a single product are loaded onto the same truck (or as few trucks as possible) - this makes it much easier to load!

            //     while keeping both of the above true, try to keep the number of trucks we need as small as possible!

            //     {totalTrucksNumber: X, truckX: {items: lineItemx, lineItemy, weight: int}}, 

            return totalTrucks;
        }

    }
}