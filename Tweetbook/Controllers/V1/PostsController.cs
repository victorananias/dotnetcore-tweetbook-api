﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Tweetbook.Contracts.v1;
using Tweetbook.Contracts.V1.Requests;
using Tweetbook.Contracts.V1.Responses;
using Tweetbook.Domain;
using Tweetbook.Extensions;
using Tweetbook.Services;

namespace Tweetbook.Controllers.v1
{
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    // [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Poster")]
    public class PostsController : ControllerBase
    {
        private readonly IPostsService _postsService;
        private readonly IMapper _mapper;

        public PostsController(IPostsService postsService, IMapper mapper)
        {
            _postsService = postsService;
            _mapper = mapper;
        }

        [AllowAnonymous]
        [HttpGet(ApiRoutes.Posts.GetAll)]
        public async Task<IActionResult> GetAll()
        {
            var posts = await _postsService.GetAllAsync();
            var response = _mapper.Map<List<PostResponse>>(posts);

            return Ok(response);
        }

        [AllowAnonymous]
        [HttpGet(ApiRoutes.Posts.Get)]
        public async Task<IActionResult> Get([FromRoute] Guid postId)
        {
            var post = await _postsService.GetPostByIdAsync(postId);

            if (post == null)
            {
                return NotFound();
            }

            return Ok(_mapper.Map<PostResponse>(post));
        }

        [Authorize(Policy = "MustWorkForTweetbook")]
        [HttpPost(ApiRoutes.Posts.Create)]
        public async Task<IActionResult> Create([FromBody] CreatePostRequest postRequest)
        {
            var newPostId = Guid.NewGuid();
            var post = new Post()
            {
                Id = newPostId,
                Name = postRequest.Name,
                UserId = HttpContext.GetUserId(),
                Tags = postRequest.Tags.Select(t => new PostTag {PostId = newPostId, TagName = t}).ToList()
            };

            await _postsService.CreateAsync(post);

            var baseUrl = $"{HttpContext.Request.Scheme}://{HttpContext.Request.Host.ToUriComponent()}";
            var locationUrl = $"{baseUrl}/{ApiRoutes.Posts.Get}".Replace("{postId}", post.Id.ToString());

            var response = _mapper.Map<PostResponse>(post);

            return Created(locationUrl, response);
        }

        [HttpPut(ApiRoutes.Posts.Update)]
        public async Task<IActionResult> Update([FromRoute] Guid postId, [FromBody] UpdatePostRequest request)
        {
            var userOwnsPost = await _postsService.UserOwnsPostAsync(postId, HttpContext.GetUserId());

            if (!userOwnsPost)
            {
                return BadRequest(new
                {
                    error = "You do not own this post."
                });
            }

            var post = await _postsService.GetPostByIdAsync(postId);
            post.Name = request.Name;

            var updated = await _postsService.UpdatePostAsync(post);

            if (!updated)
            {
                return NotFound();
            }

            var response = _mapper.Map<PostResponse>(post);

            return Ok(response);
        }

        [HttpDelete(ApiRoutes.Posts.Delete)]
        public async Task<IActionResult> Delete([FromRoute] Guid postId)
        {
            var userOwnsPost = await _postsService.UserOwnsPostAsync(postId, HttpContext.GetUserId());

            if (!userOwnsPost)
            {
                return BadRequest(new
                {
                    error = "You do not own this post."
                });
            }

            if (!await _postsService.DeletePostAsync(postId))
            {
                return NotFound();
            }

            return NoContent();
        }
    }
}