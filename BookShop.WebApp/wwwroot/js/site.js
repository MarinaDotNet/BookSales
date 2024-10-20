// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

//Adds the 'active' class to the class list the navigation button linked to the current page.
function setActiveNavButton()
{
    const currentPageId = document.body.id;

    if (!currentPageId) {
        return;
    }

    const navButtons = document.querySelectorAll(".nav-link");

    for (const button of navButtons) {
        if (`${button.id}page` === currentPageId) {
            button.classList.add("active");
            return;
        }
    }
}