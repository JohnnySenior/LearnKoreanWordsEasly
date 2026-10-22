using KoreanAiTeacher.Domain.Testing;

namespace KoreanAiTeacher.Domain.Tests.Testing;

public class BookCategoriesTests
{
    [Theory]
    [InlineData("※ [1~2] (    )에 들어갈 가장 알맞은 것을 고르십시오.", BookCategory.Blank)]
    [InlineData("※ [3~4] 밑줄 친 부분과 의미가 비슷한 것을 고르십시오.", BookCategory.Similar)]
    [InlineData("※ [5~8] 다음은 무엇에 대한 글인지 고르십시오.", BookCategory.Topic)]
    [InlineData("※ [9~12] 다음 글 또는 그래프의 내용과 같은 것을 고르십시오.", BookCategory.Content)]
    [InlineData("※ [13~15] 다음을 순서대로 맞게 배열한 것을 고르십시오.", BookCategory.Order)]
    [InlineData("※ [16~18] 다음을 읽고 (    )에 들어갈 내용으로 가장 알맞은 것을 고르십시오.", BookCategory.Blank)]
    [InlineData("※ [25~27] 다음은 신문 기사의 제목입니다. 가장 잘 설명한 것을 고르십시오.", BookCategory.Headline)]
    [InlineData("※ [35~38] 다음 글의 주제로 가장 알맞은 것을 고르십시오.", BookCategory.MainIdea)]
    [InlineData("※ [39~41] 주어진 문장이 들어가기에 가장 알맞은 곳을 고르십시오.", BookCategory.Insert)]
    [InlineData("다음을 읽고 물음에 답하십시오.", BookCategory.Other)]
    [InlineData(null, BookCategory.Other)]
    public void Standard_instructions_give_the_type(string? instruction, BookCategory expected)
    {
        Assert.Equal(expected, BookCategories.Classify(instruction));
    }

    [Fact]
    public void A_question_without_an_instruction_takes_the_type_of_the_range_that_covers_it()
    {
        var types = BookCategories.ForTest(
        [
            (1, "※ [1~2] (    )에 들어갈 가장 알맞은 것을 고르십시오.", "비가 (    ) 우산을 가지고 가세요."),
            (2, null, "주제가 어려워서 (    )."), // a gap sentence that happens to contain 주제
            (3, null, "무엇"),
            (4, "※ [3~4] 밑줄 친 부분과 의미가 비슷한 것을 고르십시오.", "..."),
        ]);

        Assert.Equal(BookCategory.Blank, types[1]);
        Assert.Equal(BookCategory.Blank, types[2]);
        Assert.Equal(BookCategory.Other, types[3]); // outside [1~2], before the next instruction
        Assert.Equal(BookCategory.Similar, types[4]);
    }

    [Fact]
    public void From_question_19_the_stem_names_the_type()
    {
        var types = BookCategories.ForTest(
        [
            (19, "※ [19~20] 다음을 읽고 물음에 답하십시오.", "(    )에 들어갈 알맞은 것을 고르십시오."),
            (20, "※ [19~20] 다음을 읽고 물음에 답하십시오.", "위 글의 내용과 같은 것을 고르십시오."),
            (23, "※ [23~24] 다음을 읽고 물음에 답하십시오.", "밑줄 친 부분에 나타난 '나'의 심정으로 알맞은 것을 고르십시오."),
            (44, "※ [44~45] 다음을 읽고 물음에 답하십시오.", "위 글의 주제로 알맞은 것을 고르십시오."),
        ]);

        Assert.Equal([BookCategory.Blank, BookCategory.Content, BookCategory.Attitude, BookCategory.MainIdea], types.Values);
    }
}
