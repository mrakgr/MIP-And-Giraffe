import * as localforage from "localforage"

export async function start() {
    try {
        await localforage.setItem('somekey','Hello')
        const value = await localforage.getItem('somekey');
        // This code runs once the value has been loaded
        // from the offline store.
        console.log(value);
    } catch (err) {
        // This code runs if there were any errors.
        console.log(err);
    }
}