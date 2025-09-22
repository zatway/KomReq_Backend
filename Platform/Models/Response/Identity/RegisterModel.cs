﻿namespace Platform.Models.Response.Identity;

public class RegisterModel
{
    public string UserName { get; set; }
    public string FullName { get; set; }
    public string Email { get; set; }
    public string Password { get; set; }
    public List<string> Roles { get; set; } = new();
}