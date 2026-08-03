// Theme and Language Management

// Run this immediately to prevent FOUC (Flash of Unstyled Content)
(function() {
    const savedTheme = localStorage.getItem('app-theme') || 'light';
    document.documentElement.setAttribute('data-theme', savedTheme);
})();

window.themeManager = {
    setTheme: function(theme) {
        document.documentElement.setAttribute('data-theme', theme);
        localStorage.setItem('app-theme', theme);
    },
    getTheme: function() {
        return localStorage.getItem('app-theme') || 'light';
    }
};

window.cultureManager = {
    get: function() {
        return window.localStorage['app-culture'];
    },
    set: function(value) {
        window.localStorage['app-culture'] = value;
        // Setting culture cookie for ASP.NET Core RequestLocalizationMiddleware
        document.cookie = `.AspNetCore.Culture=c=${value}|uic=${value};path=/;max-age=31536000`;
    }
};
