"""
ApiResult class
"""

from datetime import datetime
from typing import Dict
from discord import Embed
from api import datetime_input_format, datetime_output_format, trim_string


class ApiResult(object):
    """
    API Result object
    """

    #taken from https://rpcs3.net/compatibility
    STATUS_NOTHING = 0x455556
    STATUS_LOADABLE = 0xe74c3c
    STATUS_INTRO = 0xe08a1e
    STATUS_INGAME = 0xf9b32f
    STATUS_PLAYABLE = 0x1ebc61
    STATUS_UNKNOWN = 0x3198ff

    status_map = dict({
        "Nothing": STATUS_NOTHING,
        "Loadable": STATUS_LOADABLE,
        "Intro": STATUS_INTRO,
        "Ingame": STATUS_INGAME,
        "Playable": STATUS_PLAYABLE
    })

    def __init__(self, game_id: str, data: Dict) -> None:
        self.game_id = game_id
        self.title = data["title"] if "title" in data else None
        self.status = data["status"] if "status" in data else None
        self.date = datetime.strptime(data["date"], datetime_input_format) if "date" in data else None
        self.thread = data["thread"] if "thread" in data else None
        self.commit = data["commit"] if "commit" in data else None
        self.pr = data["pr"] if "pr" in data and data["pr"] is not 0 else """???"""

    def to_string(self) -> str:
        """
        Makes a string representation of the object.
        :return: string representation of the object
        """
        if self.status == "Maintenance":
            return "API is undergoing maintenance, please try again later."
        elif self.status in self.status_map:
            return ("ID:{:9s} Title:{:40s} PR:{:4s} Status:{:8s} Updated:{:10s}".format(
                self.game_id,
                trim_string(self.title, 40),
                self.pr,
                self.status,
                datetime.strftime(self.date, datetime_output_format)
            ))
        else:
            return "Product code {} was not found in compatibility database, possibly untested!".format(self.game_id)

    def to_embed(self, infoInFooter = True) -> Embed:
        """
        Makes an Embed representation of the object.
        :return: Embed representation of the object
        """
        if self.status == "Maintenance":
            return Embed(
                description="API is undergoing maintenance, please try again later.",
                color = 0xffff00
            )
        elif self.status in self.status_map:
            desc = "Status: {}, PR: {}, Updated: {}".format(
                self.status,
                self.pr if self.pr != "???" else """¯\_(ツ)_/¯""",
                datetime.strftime(self.date, datetime_output_format))
            result = Embed(
                title="[{}] {}".format(self.game_id, trim_string(self.title, 200)),
                url="https://forums.rpcs3.net/thread-{}.html".format(self.thread),
                description = desc if not infoInFooter else None,
                color = self.status_map[self.status])
            if infoInFooter:
                return result.set_footer(text=desc)
            else:
                return result
            
        else:
            desc = "No product id was found, log might be corrupted or tampered with"
            if self.game_id is not None:
                desc = "Product code {} was not found in compatibility database, possibly untested!".format(self.game_id)
            return Embed(
                description=desc,
                color=self.STATUS_UNKNOWN
            )
