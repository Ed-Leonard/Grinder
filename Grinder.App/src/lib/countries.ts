import countriesData from "$lib/countries.json";

export const countries = countriesData.countries;

export function countryFlag(code: string): string {
  return (
    [...code.toUpperCase()]
      // Convert each letter of the ISO country code to a Unicode regional indicator symbol ->
      // Two regional indicators together will render as a flag emoji
      .map((country) =>
        String.fromCodePoint(0x1f1e6 + country.charCodeAt(0) - 65),
      )
      .join("")
  );
}
