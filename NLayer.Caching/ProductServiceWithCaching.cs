﻿using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.SqlServer.Query.Internal;
using Microsoft.Extensions.Caching.Memory;
using NLayer.Core.DTO_s;
using NLayer.Core.Models;
using NLayer.Core.Repositories;
using NLayer.Core.Services;
using NLayer.Core.UnitOfWorks;
using NLayer.Service.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;

namespace NLayer.Caching
{
    public class ProductServiceWithCaching : IProductService
    {
        private const string CacheProductKey = "productsCache";
        private readonly IMapper _mapper;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMemoryCache _memoryCache;
        private readonly IProductRepository _repository;

        public ProductServiceWithCaching(IMapper mapper, IUnitOfWork unitOfWork, IMemoryCache memoryCache, IProductRepository repository )
        {
            _mapper = mapper;
            _unitOfWork = unitOfWork;
            _memoryCache = memoryCache;
            _repository = repository;

            if(!_memoryCache.TryGetValue(CacheProductKey, out _))
            {
                _memoryCache.Set(CacheProductKey, _repository.GetProductsWithCategory().Result);
            }
        }

        

        public async Task<Product> AddAsync(Product entity)
        {
            await _repository.AddAsync(entity);
            await _unitOfWork.CommitAsync(); 
            await CacheAllProductsAsync();
            return entity;  

            //Cachleyeceğimiz data çok sık güncellemeyeceğimiz fakat çok sık erişeceğimiz data olmalı
        }

        public async Task<IEnumerable<Product>> AddRangeAsync(IEnumerable<Product> entities)
        {
            await _repository.AddRangeAsync(entities);
            await _unitOfWork.CommitAsync();
            await CacheAllProductsAsync();
            return entities;
        }

        public Task<bool> AnyAsync(Expression<Func<Product, bool>> expression)
        {
            throw new NotImplementedException();    
        }

        public Task<IEnumerable<Product>> GetAllAsync()
        {
            var products = _memoryCache.Get<IEnumerable<Product>>(CacheProductKey);
            return Task.FromResult(products);

        }

        public Task<Product> GetByIdAsync(int id)
        { 
           var product = _memoryCache.Get<List<Product>>(CacheProductKey).FirstOrDefault(x => x.Id == id);
           if(product == null)
           {
                throw new NotFoundException($"{typeof(Product).Name}({id}) not found");
           }

           return Task.FromResult(product);
        }

        
        public Task<List<ProductWithCategoryDto>> GetProductsWithCategory()
        {
            var products = _memoryCache.Get<IEnumerable<Product>>(CacheProductKey);

            var productsWithCategoryDto = _mapper.Map<List<ProductWithCategoryDto>>(products);

            return Task.FromResult(productsWithCategoryDto);
        }

        public async Task RemoveAsync(Product entity)
        {
            _repository.Remove(entity);
            await _unitOfWork.CommitAsync();
            await CacheAllProductsAsync();
            
        }

        public async Task RemoveRangeAsync(IEnumerable<Product> entities)
        {
            _repository.RemoveRange(entities);
            await _unitOfWork.CommitAsync();
            await CacheAllProductsAsync();  
        }

        public async Task UpdateAsync(Product entity)
        {
            _repository.Update(entity); 
            await _unitOfWork.CommitAsync();
            await CacheAllProductsAsync();  
        }

        public IQueryable<Product> Where(Expression<Func<Product, bool>> expression)
        {
            return _memoryCache.Get<List<Product>>(CacheProductKey).Where(expression.Compile()).AsQueryable();
        }

        public async Task CacheAllProductsAsync()
        {
            _memoryCache.Set(CacheProductKey, await _repository.GetAll().ToListAsync()); 
            //Bu metot her çağrıldığında datayı sıfırdan çekip cacheliyor.

            
        }
    }
}
