# Optimizely-New-Customers
C# Program that uses Rest API calls to Optimizely B2B Configured Commerce v1 admin api to get a list of new accounts made in a user inputted month and year

Simple program that essentially simplifies a few API calls. This C# program generates an AppData folder for the user to put their Optimizely login and company url into.
I had plans to implement integration with COnstant Contact to be able to directly upload the contacts to a new list on the users account but their OAuth 2.0 was more complex
and I didn't get time to get to it.

Users are prompted to put in their login to the generated credential files in the format username:USERNAME password:PASSWORD and url:www.company.com
and then they are prompted to choose a month and year, the program then gets the customer list and outputs it to a tab delimited text file which can be easily viewed in excel
